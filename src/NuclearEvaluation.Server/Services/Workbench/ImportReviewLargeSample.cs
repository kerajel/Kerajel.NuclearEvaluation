using DuckDB.NET.Data;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Workbench;

/// <summary>Stages RadNet CSV files into one disk-backed database per process.</summary>
public sealed class ImportReviewLargeSample : IAsyncDisposable
{
    internal const string Header =
        "id,source_file_id,source_row,observed_at,status,exposure_mr_h,dose_nsv_h,r02,r03,r04,r05,"
        + "r06,r07,r08,r09,station_id,station_name,state,year,file_name";

    readonly DirectoryInfo _source;
    readonly string? _fallbackSnapshot;
    readonly long _minimumFallbackRows;
    readonly SemaphoreSlim _gate = new(1, 1);
    StagedSample? _sample;

    public ImportReviewLargeSample(IConfiguration configuration, IHostEnvironment environment)
        : this(
            Path.GetFullPath(
                configuration["ImportReview:LargeSampleDirectory"]
                    ?? "App_Data/radnet-large-sample",
                environment.ContentRootPath
            ),
            environment.IsProduction() && configuration["ImportReview:LargeSampleDirectory"] is null
                ? Path.GetFullPath(
                    configuration["RadNet:SnapshotPath"] ?? "App_Data/radnet.duckdb",
                    environment.ContentRootPath
                )
                : null
        ) { }

    internal ImportReviewLargeSample(
        string path,
        string? fallbackSnapshot = null,
        long minimumFallbackRows = 10_000_000
    )
    {
        _source = new(path);
        _fallbackSnapshot = fallbackSnapshot;
        _minimumFallbackRows = minimumFallbackRows;
    }

    public bool IsAvailable
    {
        get
        {
            try
            {
                return Directory.Exists(_source.FullName)
                    ? _source.EnumerateFiles("*.csv").Any()
                    : File.Exists(_fallbackSnapshot);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    public async Task<ImportReviewLargeStage?> StageAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_sample is { } staged)
                return staged.Stage;
            if (!Directory.Exists(_source.FullName))
            {
                if (_fallbackSnapshot is not { } snapshot || !File.Exists(snapshot))
                    return null;

                await ImportReviewLargeSampleExport.ExportAsync(
                    snapshot,
                    _source,
                    _minimumFallbackRows,
                    ct
                );
            }

            FileInfo[] files = _source
                .EnumerateFiles("*.csv")
                .OrderBy(file => file.Name, StringComparer.Ordinal)
                .ToArray();
            if (files.Length == 0)
                return null;

            foreach (FileInfo file in files)
            {
                ct.ThrowIfCancellationRequested();
                // DuckDB interprets wildcard characters in CSV paths as file patterns.
                if (file.FullName.IndexOfAny(['*', '?', '[']) >= 0)
                    throw new InvalidDataException("A large RadNet CSV path is not supported.");
                using StreamReader source = file.OpenText();
                if (await source.ReadLineAsync(ct) != Header)
                    throw new InvalidDataException("A large RadNet CSV has unexpected columns.");
            }

            DirectoryInfo directory = Directory.CreateTempSubdirectory("nuclear-radnet-review-");
            string database = Path.Combine(directory.FullName, "radnet.duckdb");
            try
            {
                ImportReviewLargeStage stage = await LoadAsync(
                    files,
                    database,
                    directory.FullName,
                    ct
                );
                ct.ThrowIfCancellationRequested();
                _sample = new(stage, new RadNetSnapshot(database), directory);
                return stage;
            }
            catch
            {
                directory.Delete(recursive: true);
                throw;
            }
        }
        catch (DuckDBException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (Exception error)
            when (error is DuckDBException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                "The large RadNet CSV files could not be staged.",
                error
            );
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RadNetQueryResult?> QueryAsync(RadNetQuery query, CancellationToken ct)
    {
        if (query.Entity != RadNetEntity.Observation)
            throw new ArgumentException("Choose observation results for the staged RadNet CSV.");

        await _gate.WaitAsync(ct);
        try
        {
            return _sample is { } sample ? await sample.Snapshot.QueryAsync(query, ct) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    async Task<ImportReviewLargeStage> LoadAsync(
        FileInfo[] files,
        string database,
        string directory,
        CancellationToken ct
    )
    {
        await using DuckDBConnection connection = new(
            new DuckDBConnectionStringBuilder
            {
                DataSource = database,
                Threads = 2,
                MemoryLimit = "512MB",
                TempDirectory = Path.Combine(directory, "spill"),
                MaxTempDirectorySize = "2GB",
            }.ConnectionString
        );
        await connection.OpenAsync(ct);

        await using (DuckDBCommand load = (DuckDBCommand)connection.CreateCommand())
        {
            load.CommandText = """
                CREATE TABLE observations AS
                    SELECT * FROM read_csv($paths, header=true, auto_detect=false, delim=',',
                        strict_mode=true, force_not_null=['status'], columns={
                            'id': 'BIGINT', 'source_file_id': 'INTEGER', 'source_row': 'INTEGER',
                            'observed_at': 'TIMESTAMP', 'status': 'VARCHAR',
                            'exposure_mr_h': 'DOUBLE', 'dose_nsv_h': 'DOUBLE',
                            'r02': 'DOUBLE', 'r03': 'DOUBLE', 'r04': 'DOUBLE', 'r05': 'DOUBLE',
                            'r06': 'DOUBLE', 'r07': 'DOUBLE', 'r08': 'DOUBLE', 'r09': 'DOUBLE',
                            'station_id': 'INTEGER', 'station_name': 'VARCHAR', 'state': 'VARCHAR',
                            'year': 'INTEGER', 'file_name': 'VARCHAR'
                        })
                """;
            load.Parameters.Add(
                new DuckDBParameter("paths", files.Select(file => file.FullName).ToArray())
            );
            await load.ExecuteNonQueryAsync(ct);
        }

        await using DuckDBCommand dimensions = (DuckDBCommand)connection.CreateCommand();
        dimensions.CommandText = """
            CREATE TABLE stations AS
                SELECT station_id AS id, min(station_name) AS name, min(state) AS state,
                       NULL::DOUBLE AS latitude, NULL::DOUBLE AS longitude
                FROM observations GROUP BY station_id;
            CREATE TABLE source_files AS
                SELECT source_file_id AS id, min(station_id) AS station_id, min(year) AS year,
                       min(file_name) AS file_name, count(*) AS row_count,
                       NULL::VARCHAR AS archive_url, NULL::VARCHAR AS archive_sha256,
                       NULL::BIGINT AS source_rows, NULL::BIGINT AS rejected_rows,
                       NULL::BIGINT AS duplicate_rows
                FROM observations GROUP BY source_file_id;
            """;
        await dimensions.ExecuteNonQueryAsync(ct);

        await using DuckDBCommand counts = (DuckDBCommand)connection.CreateCommand();
        counts.CommandText = "SELECT coalesce(sum(row_count), 0)::BIGINT FROM source_files";
        long rows = (long)(await counts.ExecuteScalarAsync(ct) ?? 0L);
        if (rows == 0)
            throw new InvalidDataException("The large RadNet CSV files contain no data rows.");

        return new(_source.Name, files.Sum(file => file.Length), rows, files.Length);
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _sample?.Directory.Delete(recursive: true);
            _sample = null;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    sealed record StagedSample(
        ImportReviewLargeStage Stage,
        RadNetSnapshot Snapshot,
        DirectoryInfo Directory
    );
}
