using System.Data.Common;
using DuckDB.NET.Data;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.RemDb;

public sealed class RemDbSnapshot
{
    readonly string _path;

    public RemDbSnapshot(IConfiguration configuration, IHostEnvironment environment)
        : this(
            Path.GetFullPath(
                configuration["RemDb:SnapshotPath"] ?? "App_Data/remdb.duckdb",
                environment.ContentRootPath
            )
        ) { }

    internal RemDbSnapshot(string path) => _path = path;

    internal bool IsAvailable => File.Exists(_path);

    internal async Task<RemDbSummary> SummaryAsync(CancellationToken cancellationToken)
    {
        await using DuckDBConnection? connection = await OpenAsync(cancellationToken);
        if (connection is null)
            return new(false, 0, 0, 0, 0, [], [], new Dictionary<string, string>());

        await using DuckDBCommand counts = Command(
            connection,
            """
            SELECT (SELECT count(*) FROM sites), (SELECT count(*) FROM sampling_windows),
                coalesce(sum(row_count), 0)::BIGINT, count(*)
            FROM source_files
            """
        );
        long siteCount;
        long windowCount;
        long measurementCount;
        long fileCount;
        await using (DbDataReader reader = await counts.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            siteCount = reader.GetInt64(0);
            windowCount = reader.GetInt64(1);
            measurementCount = reader.GetInt64(2);
            fileCount = reader.GetInt64(3);
        }

        await using DuckDBCommand years = Command(
            connection,
            """
            SELECT year, sum(row_count)::BIGINT FROM source_files GROUP BY year ORDER BY year
            """
        );
        List<RemDbYearCoverage> coverage = [];
        await using (DbDataReader reader = await years.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                coverage.Add(new(reader.GetInt32(0), reader.GetInt64(1)));
        }

        await using DuckDBCommand countriesQuery = Command(
            connection,
            """
            SELECT country, count(*) FROM sites WHERE nullif(trim(country), '') IS NOT NULL
            GROUP BY country ORDER BY country
            """
        );
        List<RemDbCountryCoverage> countries = [];
        await using (
            DbDataReader reader = await countriesQuery.ExecuteReaderAsync(cancellationToken)
        )
        {
            while (await reader.ReadAsync(cancellationToken))
                countries.Add(new(reader.GetString(0), reader.GetInt64(1)));
        }

        await using DuckDBCommand metadataQuery = Command(
            connection,
            """SELECT "key", "value" FROM snapshot_metadata"""
        );
        Dictionary<string, string> metadata = [];
        await using (
            DbDataReader reader = await metadataQuery.ExecuteReaderAsync(cancellationToken)
        )
        {
            while (await reader.ReadAsync(cancellationToken))
                metadata[reader.GetString(0)] = reader.GetString(1);
        }
        return new(
            true,
            siteCount,
            windowCount,
            measurementCount,
            fileCount,
            coverage,
            countries,
            metadata
        );
    }

    internal async Task<RemDbQueryResult?> QueryAsync(
        RemDbQuery query,
        CancellationToken cancellationToken
    )
    {
        RemDbQueryPlan plan = RemDbQueryPlan.Compile(query);
        await using DuckDBConnection? connection = await OpenAsync(cancellationToken);
        if (connection is null)
            return null;

        await using DuckDBCommand countCommand = Command(connection, plan.CountSql, plan.Arguments);
        long count = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (query.Skip >= count)
            return new([], count);

        await using DuckDBCommand pageCommand = Command(connection, plan.PageSql, plan.Arguments);
        pageCommand.Parameters.Add(new DuckDBParameter("top", query.Top));
        pageCommand.Parameters.Add(new DuckDBParameter("skip", query.Skip));
        await using DbDataReader reader = await pageCommand.ExecuteReaderAsync(cancellationToken);
        List<RemDbRow> items = new(query.Top);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(ReadRow(reader));
        return new(items, count);
    }

    internal async Task<RemDbFilePage?> FilesAsync(
        int skip,
        int top,
        CancellationToken cancellationToken
    )
    {
        RemDbQueryPlan.ValidatePage(skip, top);
        await using DuckDBConnection? connection = await OpenAsync(cancellationToken);
        if (connection is null)
            return null;

        await using DuckDBCommand countCommand = Command(
            connection,
            "SELECT count(*) FROM source_files"
        );
        long count = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (skip >= count)
            return new([], count);

        await using DuckDBCommand pageCommand = Command(
            connection,
            """
            SELECT id, year, archive_url, archive_sha256, file_name, row_count
            FROM source_files ORDER BY year DESC, file_name, id LIMIT $top OFFSET $skip
            """
        );
        pageCommand.Parameters.Add(new DuckDBParameter("top", top));
        pageCommand.Parameters.Add(new DuckDBParameter("skip", skip));
        await using DbDataReader reader = await pageCommand.ExecuteReaderAsync(cancellationToken);
        List<RemDbSourceFile> items = new(top);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(ReadFile(reader));
        return new(items, count);
    }

    internal async Task<RemDbFileQuality?> FileQualityAsync(
        int sourceFileId,
        CancellationToken cancellationToken
    )
    {
        await using DuckDBConnection? connection = await OpenAsync(cancellationToken);
        if (connection is null)
            return null;

        await using DuckDBCommand fileQuery = Command(
            connection,
            """
            SELECT id, year, archive_url, archive_sha256, file_name, row_count
            FROM source_files WHERE id = $id
            """
        );
        fileQuery.Parameters.Add(new DuckDBParameter("id", sourceFileId));
        RemDbSourceFile sourceFile;
        await using (DbDataReader reader = await fileQuery.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            sourceFile = ReadFile(reader);
        }

        await using DuckDBCommand qualityQuery = Command(
            connection,
            """
            SELECT count(*), count(*) FILTER (WHERE m.activity IS NULL),
                count(*) FILTER (WHERE trim(m.qualifier) IN ('<', '<=')),
                count(*) FILTER (WHERE nullif(trim(m.qualifier), '') IS NULL),
                min(w.began_on), max(w.ended_on)
            FROM measurements m JOIN sampling_windows w ON w.id = m.window_id
            WHERE m.source_file_id = $id
            """
        );
        qualityQuery.Parameters.Add(new DuckDBParameter("id", sourceFileId));
        long count;
        long missingActivity;
        long lessThan;
        long missingQualifier;
        DateOnly? firstBeganOn;
        DateOnly? lastEndedOn;
        await using (DbDataReader reader = await qualityQuery.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            count = reader.GetInt64(0);
            missingActivity = reader.GetInt64(1);
            lessThan = reader.GetInt64(2);
            missingQualifier = reader.GetInt64(3);
            firstBeganOn = Date(reader, 4);
            lastEndedOn = Date(reader, 5);
        }

        await using DuckDBCommand coverageQuery = Command(
            connection,
            """
            SELECT unit, value_type, grouping(unit) AS grouped_unit, count(*) AS n
            FROM measurements WHERE source_file_id = $id
            GROUP BY GROUPING SETS ((unit), (value_type))
            ORDER BY grouped_unit, n DESC, unit, value_type
            """
        );
        coverageQuery.Parameters.Add(new DuckDBParameter("id", sourceFileId));
        await using DbDataReader coverage = await coverageQuery.ExecuteReaderAsync(
            cancellationToken
        );
        List<RemDbCoverageValue> units = [];
        List<RemDbCoverageValue> resultTypes = [];
        while (await coverage.ReadAsync(cancellationToken))
        {
            bool isUnit = coverage.GetInt64(2) == 0;
            string? value = Text(coverage, isUnit ? 0 : 1);
            if (!string.IsNullOrWhiteSpace(value))
                (isUnit ? units : resultTypes).Add(new(value, coverage.GetInt64(3)));
        }
        return new(
            sourceFile,
            count,
            missingActivity,
            lessThan,
            missingQualifier,
            firstBeganOn,
            lastEndedOn,
            units,
            resultTypes
        );
    }

    async Task<DuckDBConnection?> OpenAsync(CancellationToken cancellationToken)
    {
        if (!IsAvailable)
            return null;
        string snapshotPath = File.ResolveLinkTarget(_path, returnFinalTarget: true)?.FullName
            ?? _path;
        DuckDBConnectionStringBuilder settings = new()
        {
            DataSource = snapshotPath,
            AccessMode = DuckDBAccessMode.ReadOnly,
            EnableExternalAccess = false,
            Threads = 2,
            MemoryLimit = "512MB",
        };
        DuckDBConnection connection = new(settings.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    static DuckDBCommand Command(
        DuckDBConnection connection,
        string sql,
        IReadOnlyDictionary<string, object>? arguments = null
    )
    {
        DuckDBCommand command = (DuckDBCommand)connection.CreateCommand();
        command.CommandText = sql;
        if (arguments is not null)
            foreach ((string key, object value) in arguments)
                command.Parameters.Add(new DuckDBParameter(key, value));
        return command;
    }

    static RemDbSourceFile ReadFile(DbDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt64(5)
        );

    static RemDbRow ReadRow(DbDataReader reader) =>
        new()
        {
            SiteId = reader.GetInt32(0),
            Country = Text(reader, 1) ?? "",
            SiteName = Text(reader, 2) ?? "",
            Latitude = Number(reader, 3),
            Longitude = Number(reader, 4),
            WindowId = reader.IsDBNull(5) ? null : reader.GetInt64(5),
            Medium = Text(reader, 6),
            Treatment = Text(reader, 7),
            BeganOn = Date(reader, 8),
            EndedOn = Date(reader, 9),
            SourceBegin = Text(reader, 10),
            SourceEnd = Text(reader, 11),
            MeasurementId = reader.IsDBNull(12) ? null : reader.GetInt64(12),
            SourceFileId = reader.IsDBNull(13) ? null : reader.GetInt32(13),
            SourceRow = reader.IsDBNull(14) ? null : reader.GetInt32(14),
            SourceYear = reader.IsDBNull(15) ? null : reader.GetInt32(15),
            ArchiveUrl = Text(reader, 16),
            ArchiveSha256 = Text(reader, 17),
            FileName = Text(reader, 18),
            Laboratory = Text(reader, 19),
            Apparatus = Text(reader, 20),
            Nuclide = Text(reader, 21),
            Activity = Number(reader, 22),
            Unit = Text(reader, 23),
            Qualifier = Text(reader, 24),
            ValueType = Text(reader, 25),
        };

    static string? Text(DbDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetString(index);

    static double? Number(DbDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetDouble(index);

    static DateOnly? Date(DbDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : DateOnly.FromDateTime(reader.GetDateTime(index));
}
