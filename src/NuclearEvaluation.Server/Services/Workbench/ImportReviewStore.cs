using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using CsvHelper;
using DuckDB.NET.Data;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Workbench;

/// <summary>Holds the latest uploaded annual file per workspace for temporary review.</summary>
public sealed class ImportReviewStore(RadNetSnapshot snapshot) : IDisposable
{
    const int MaxStages = 16;
    const int MaxRows = 50_000;
    const int MaxFieldLength = 256;
    const int BatchSize = 1024;

    static readonly Dictionary<string, object> NoArguments = [];
    static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(15);

    static readonly string[] MeasurementColumns =
    [
        "exposure_mr_h",
        "dose_nsv_h",
        "r02",
        "r03",
        "r04",
        "r05",
        "r06",
        "r07",
        "r08",
        "r09",
    ];
    static readonly string Measurements = string.Join(", ", MeasurementColumns);

    readonly SemaphoreSlim _gate = new(1, 1);
    readonly Dictionary<Guid, StageEntry> _stages = [];

    public async Task<ImportReviewStage> StageAsync(
        Guid workspace,
        ImportReviewSource source,
        string name,
        Stream csv,
        CancellationToken ct
    )
    {
        using MemoryStream input = await ReadUploadAsync(csv, ct);
        string sha256 = Convert.ToHexStringLower(
            SHA256.HashData(input.GetBuffer().AsSpan(0, (int)input.Length))
        );
        await _gate.WaitAsync(ct);
        try
        {
            ExpireStages();
            if (
                _stages.TryGetValue(workspace, out StageEntry? existing)
                && existing.Stage.Source == source
                && existing.Stage.Sha256 == sha256
            )
            {
                existing.LastAccess = DateTimeOffset.UtcNow;
                return existing.Stage;
            }

            DuckDBConnection database = new(
                new DuckDBConnectionStringBuilder
                {
                    DataSource = ":memory:",
                    EnableExternalAccess = false,
                    Threads = 1,
                    MemoryLimit = "96MB",
                }.ConnectionString
            );
            try
            {
                await database.OpenAsync(ct);
                await LoadInputAsync(database, input, ct);
                await LoadArchiveAsync(database, source, ct);
                await CompareAsync(database, source, ct);
                ImportReviewStage stage = await ReadStageAsync(database, source, name, sha256, ct);

                RemoveStage(workspace);
                if (_stages.Count >= MaxStages)
                    RemoveStage(_stages.MinBy(entry => entry.Value.LastAccess).Key);

                _stages.Add(workspace, new(stage, database));
                return stage;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ImportReviewStage?> FindAsync(Guid workspace, Guid id, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return GetStage(workspace, id)?.Stage;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ImportReviewPage?> QueryAsync(
        Guid workspace,
        Guid id,
        ImportReviewQuery query,
        CancellationToken ct
    )
    {
        await _gate.WaitAsync(ct);
        try
        {
            return GetStage(workspace, id) is { } entry
                ? await ImportReviewQueryPlan.ReadAsync(entry.Database, query, ct)
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    StageEntry? GetStage(Guid workspace, Guid id)
    {
        ExpireStages();
        if (!_stages.TryGetValue(workspace, out StageEntry? entry) || entry.Stage.Id != id)
            return null;

        entry.LastAccess = DateTimeOffset.UtcNow;
        return entry;
    }

    void ExpireStages()
    {
        DateTimeOffset expires = DateTimeOffset.UtcNow - IdleTimeout;
        foreach (
            Guid workspace in _stages
                .Where(x => x.Value.LastAccess <= expires)
                .Select(x => x.Key)
                .ToArray()
        )
            RemoveStage(workspace);
    }

    void RemoveStage(Guid workspace)
    {
        if (_stages.Remove(workspace, out StageEntry? entry))
            entry.Database.Dispose();
    }

    public void Dispose()
    {
        foreach (StageEntry entry in _stages.Values)
            entry.Database.Dispose();
        _stages.Clear();
        _gate.Dispose();
    }

    static async Task<MemoryStream> ReadUploadAsync(Stream csv, CancellationToken ct)
    {
        MemoryStream input = new();
        try
        {
            byte[] buffer = new byte[8192];
            int size;
            while ((size = await csv.ReadAsync(buffer, ct)) != 0)
            {
                if (input.Length + size > ImportReviewCatalog.MaxUploadBytes)
                    throw new ArgumentException("Choose a RadNet CSV file no larger than 8 MB.");

                input.Write(buffer, 0, size);
            }

            input.Position = 0;
            return input;
        }
        catch
        {
            input.Dispose();
            throw;
        }
    }

    static async Task LoadInputAsync(DuckDBConnection database, Stream input, CancellationToken ct)
    {
        await ExecuteAsync(
            database,
            $"""
            CREATE TABLE raw_rows (
                source_row INTEGER, location VARCHAR, collection_time VARCHAR, status VARCHAR,
                {string.Join(", ", MeasurementColumns.Select(column => column + " VARCHAR"))}
            )
            """,
            NoArguments,
            ct
        );

        using CsvReader reader = CsvReader.Create(
            input,
            new CsvReaderOptions
            {
                Separator = ',',
                DetectSeparatorHint = false,
                SkipEmptyLines = false,
                MalformedData = CsvMalformedDataHandling.Throw,
                MissingField = CsvMissingFieldHandling.Throw,
                IncludeValuesInExceptions = false,
            }
        );
        try
        {
            await reader.ReadHeaderAsync(ct);
            IReadOnlyList<string> headers = reader.Headers;
            if (headers.Distinct(StringComparer.Ordinal).Count() != headers.Count)
                throw new ArgumentException("The CSV contains duplicate column names.");

            int location = ColumnIndex(headers, "LOCATION_NAME");
            int collectionTime = ColumnIndex(headers, "SAMPLE COLLECTION TIME");
            int status = ColumnIndex(headers, "STATUS");
            int[] measurements =
            [
                ColumnIndex(headers, "EXPOSURE RATE (mR/h)", "EXPOSURE RATE (mR/hr)"),
                ColumnIndex(headers, "DOSE EQUIVALENT RATE (nSv/h)"),
                .. MeasurementColumns
                    .Skip(2)
                    .Select(column =>
                        ColumnIndex(headers, $"GAMMA COUNT RATE {column.ToUpperInvariant()} (CPM)")
                    ),
            ];
            if (location < 0 || collectionTime < 0 || measurements.All(index => index < 0))
                throw new ArgumentException(
                    "The CSV must have RadNet location, collection time, and measurement columns."
                );

            int sourceRow = 1;
            List<RawRow> batch = new(BatchSize);
            while (await reader.ReadAsync(ct))
            {
                sourceRow++;
                if (sourceRow > MaxRows + 1)
                    throw new ArgumentException($"The CSV has more than {MaxRows:N0} records.");
                if (reader.RawRecord.Length == 0)
                    continue;
                if (reader.FieldCount != headers.Count)
                    throw new ArgumentException(
                        $"CSV record {sourceRow} does not have the same number of fields as the header."
                    );

                batch.Add(
                    new(
                        sourceRow,
                        ReadField(reader, location),
                        ReadField(reader, collectionTime),
                        ReadField(reader, status),
                        measurements.Select(index => ReadField(reader, index)).ToArray()
                    )
                );
                if (batch.Count == BatchSize)
                    await InsertRawAsync(database, batch, ct);
            }

            await InsertRawAsync(database, batch, ct);
        }
        catch (CsvFormatException)
        {
            throw new ArgumentException("The file does not contain well-formed RadNet CSV.");
        }
    }

    static int ColumnIndex(IReadOnlyList<string> headers, params string[] names)
    {
        foreach (string name in names)
            for (int index = 0; index < headers.Count; index++)
                if (headers[index] == name)
                    return index;
        return -1;
    }

    static string? ReadField(CsvReader reader, int index)
    {
        string? value = index < 0 ? null : reader.GetString(index);
        if (value is { Length: > MaxFieldLength })
            throw new ArgumentException("A RadNet field is longer than 256 characters.");
        return value;
    }

    static Task InsertRawAsync(
        DuckDBConnection database,
        List<RawRow> batch,
        CancellationToken ct
    ) =>
        InsertBatchAsync(
            database,
            $"""
            INSERT INTO raw_rows
            SELECT (value ->> 'SourceRow')::INTEGER, value ->> 'Location',
                value ->> 'CollectionTime', value ->> 'Status', {MeasurementJson()}
            FROM json_each($rows)
            """,
            batch,
            ct
        );

    async Task LoadArchiveAsync(
        DuckDBConnection database,
        ImportReviewSource source,
        CancellationToken ct
    )
    {
        await using DuckDBConnection archive = await snapshot.OpenAsync(ct)
            ?? throw new InvalidOperationException("The RadNet archive is unavailable.");
        await using DuckDBCommand reference = RadNetSnapshot.Command(
            archive,
            $"""
            SELECT o.observed_at, o.status, {Measurements}
            FROM source_files f
            JOIN stations s ON s.id = f.station_id
            JOIN observations o ON o.source_file_id = f.id
            WHERE f.id = $file AND f.year = $year AND f.row_count = $count
                AND f.file_name = $name AND s.state = $state AND s.name = $station
                AND ($sha = '' OR f.archive_sha256 = $sha)
            """,
            new Dictionary<string, object>
            {
                ["file"] = source.FileId,
                ["year"] = source.Year,
                ["count"] = source.ObservationCount,
                ["name"] = source.FileName,
                ["state"] = source.State,
                ["station"] = source.Station,
                ["sha"] = source.ArchiveSha256,
            }
        );
        await ExecuteAsync(
            database,
            $"""
            CREATE TABLE archive_rows (
                observed_at TIMESTAMP PRIMARY KEY, status VARCHAR,
                {string.Join(", ", MeasurementColumns.Select(column => column + " DOUBLE"))}
            )
            """,
            NoArguments,
            ct
        );

        await using DbDataReader reader = await reference.ExecuteReaderAsync(ct);
        List<ArchiveRow> batch = new(BatchSize);
        long count = 0;
        while (await reader.ReadAsync(ct))
        {
            double?[] values = new double?[MeasurementColumns.Length];
            for (int index = 0; index < values.Length; index++)
                values[index] = reader.IsDBNull(index + 2) ? null : reader.GetDouble(index + 2);

            batch.Add(new(reader.GetDateTime(0), reader.GetString(1), values));
            count++;
            if (batch.Count == BatchSize)
                await InsertArchiveAsync(database, batch, ct);
        }

        if (count == 0 || count != source.ObservationCount)
            throw new InvalidOperationException(
                "The selected RadNet source is unavailable in the archive."
            );
        await InsertArchiveAsync(database, batch, ct);
    }

    static Task InsertArchiveAsync(
        DuckDBConnection database,
        List<ArchiveRow> batch,
        CancellationToken ct
    ) =>
        InsertBatchAsync(
            database,
            $"""
            INSERT INTO archive_rows
            SELECT (value ->> 'ObservedAt')::TIMESTAMP, value ->> 'Status', {MeasurementJson(
                "::DOUBLE"
            )}
            FROM json_each($rows)
            """,
            batch,
            ct
        );

    static string MeasurementJson(string cast = "") =>
        string.Join(
            ", ",
            Enumerable
                .Range(0, MeasurementColumns.Length)
                .Select(index => $"(value ->> '$.Values[{index}]'){cast}")
        );

    static async Task InsertBatchAsync<TRow>(
        DuckDBConnection database,
        string sql,
        List<TRow> batch,
        CancellationToken ct
    )
    {
        if (batch.Count == 0)
            return;
        await ExecuteAsync(
            database,
            sql,
            new Dictionary<string, object> { ["rows"] = JsonSerializer.Serialize(batch) },
            ct
        );
        batch.Clear();
    }

    static Task CompareAsync(
        DuckDBConnection database,
        ImportReviewSource source,
        CancellationToken ct
    )
    {
        string finiteValues = string.Join(
            ", ",
            MeasurementColumns.Select(column =>
                $"CASE WHEN isfinite(try_cast({column} AS DOUBLE)) THEN try_cast({column} AS DOUBLE) END AS {column}"
            )
        );
        string completeness = string.Join(
            " + ",
            MeasurementColumns.Select(column => $"({column} IS NOT NULL)::INTEGER")
        );
        string matches = string.Join(
            " AND ",
            MeasurementColumns
                .Prepend("status")
                .Select(column => $"u.{column} IS NOT DISTINCT FROM a.{column}")
        );
        return ExecuteAsync(
            database,
            $"""
            CREATE TABLE review_rows AS
            WITH normalized AS (
                SELECT source_row, trim(location) AS location,
                    CASE WHEN regexp_matches(trim(collection_time), '^[0-9]') THEN
                        coalesce(
                            try_strptime(trim(collection_time), ['%m/%d/%Y %H:%M:%S', '%m/%d/%Y %H:%M']),
                            try_cast(trim(collection_time) AS TIMESTAMP)
                        )
                    END AS observed_at,
                    coalesce(upper(trim(status)), '') AS status,
                    {finiteValues}
                FROM raw_rows
            ), checked AS (
                SELECT *, CASE
                    WHEN upper(location) IS DISTINCT FROM upper($location)
                        THEN 'Location differs from the selected archive file.'
                    WHEN observed_at IS NULL OR NOT isfinite(observed_at) THEN 'Missing or invalid collection time.'
                    WHEN year(observed_at) <> $year THEN 'Collection time falls outside the archive year.'
                    WHEN coalesce({Measurements}) IS NULL THEN 'No finite measurement values.'
                END AS rejection
                FROM normalized
            ), ranked AS (
                SELECT source_row, first_value(source_row) OVER (
                    PARTITION BY observed_at
                    ORDER BY (status = 'APPROVED') DESC, ({completeness}) DESC, source_row
                ) AS preferred_row
                FROM checked
                WHERE rejection IS NULL
            ), classified AS (
                SELECT u.source_row, coalesce(u.location, '') AS location,
                    CASE WHEN year(u.observed_at) BETWEEN 1 AND 9999 THEN u.observed_at END AS observed_at,
                    u.status, u.exposure_mr_h, u.dose_nsv_h, u.r03,
                    CASE
                        WHEN u.rejection IS NOT NULL THEN '{nameof(
                ImportReviewDisposition.Rejected
            )}'
                        WHEN ranked.preferred_row <> u.source_row THEN '{nameof(
                ImportReviewDisposition.Duplicate
            )}'
                        WHEN a.observed_at IS NULL THEN '{nameof(ImportReviewDisposition.New)}'
                        WHEN {matches} THEN '{nameof(ImportReviewDisposition.MatchesArchive)}'
                        ELSE '{nameof(ImportReviewDisposition.Changed)}'
                    END AS disposition,
                    u.rejection, ranked.preferred_row
                FROM checked u
                LEFT JOIN ranked USING (source_row)
                LEFT JOIN archive_rows a ON a.observed_at = u.observed_at AND ranked.preferred_row = u.source_row
            )
            SELECT source_row, location, observed_at, status, exposure_mr_h, dose_nsv_h, r03, disposition,
                CASE disposition
                    WHEN '{nameof(ImportReviewDisposition.Rejected)}' THEN rejection
                    WHEN '{nameof(ImportReviewDisposition.Duplicate)}' THEN
                        'Collection time is represented by CSV record ' || preferred_row || '.'
                    WHEN '{nameof(
                ImportReviewDisposition.New
            )}' THEN 'Collection time is absent from the archive.'
                    WHEN '{nameof(
                ImportReviewDisposition.Changed
            )}' THEN 'Status or measurement values differ from the archive.'
                    ELSE 'Status and all measurement values match the archive.'
                END AS reason
            FROM classified
            """,
            new Dictionary<string, object>
            {
                ["location"] = source.SourceLocation,
                ["year"] = source.Year,
            },
            ct
        );
    }

    static async Task<ImportReviewStage> ReadStageAsync(
        DuckDBConnection database,
        ImportReviewSource source,
        string name,
        string sha256,
        CancellationToken ct
    )
    {
        ImportReviewStage stage;
        await using (
            DuckDBCommand command = RadNetSnapshot.Command(
                database,
                $"""
                SELECT count(*),
                    count(*) FILTER (WHERE disposition = '{nameof(
                    ImportReviewDisposition.MatchesArchive
                )}'),
                    count(*) FILTER (WHERE disposition = '{nameof(
                    ImportReviewDisposition.Duplicate
                )}'),
                    count(*) FILTER (WHERE disposition = '{nameof(
                    ImportReviewDisposition.Rejected
                )}'),
                    count(*) FILTER (WHERE disposition = '{nameof(
                    ImportReviewDisposition.Changed
                )}'),
                    count(*) FILTER (WHERE disposition = '{nameof(ImportReviewDisposition.New)}'),
                    count(*) FILTER (WHERE exposure_mr_h IS NULL)
                FROM review_rows
                """,
                NoArguments
            )
        )
        {
            await using DbDataReader reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            long inputRows = reader.GetInt64(0);
            if (inputRows == 0)
                throw new ArgumentException("The CSV contains no RadNet records.");

            long matched = reader.GetInt64(1);
            long changed = reader.GetInt64(4);
            string fileName = Path.GetFileName(name.Replace('\\', '/')).Trim();
            stage = new(
                Guid.NewGuid(),
                source,
                fileName[..Math.Min(fileName.Length, 160)],
                sha256,
                inputRows,
                matched,
                reader.GetInt64(2),
                reader.GetInt64(3),
                changed,
                reader.GetInt64(5),
                source.ObservationCount - matched - changed,
                reader.GetInt64(6)
            );
        }

        await ExecuteAsync(database, "DROP TABLE raw_rows", NoArguments, ct);
        await ExecuteAsync(database, "DROP TABLE archive_rows", NoArguments, ct);
        return stage;
    }

    static async Task ExecuteAsync(
        DuckDBConnection database,
        string sql,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken ct
    )
    {
        await using DuckDBCommand command = RadNetSnapshot.Command(database, sql, arguments);
        await command.ExecuteNonQueryAsync(ct);
    }

    sealed record RawRow(
        int SourceRow,
        string? Location,
        string? CollectionTime,
        string? Status,
        string?[] Values
    );

    sealed record ArchiveRow(DateTime ObservedAt, string Status, double?[] Values);

    sealed record StageEntry(ImportReviewStage Stage, DuckDBConnection Database)
    {
        public DateTimeOffset LastAccess { get; set; } = DateTimeOffset.UtcNow;
    }
}
