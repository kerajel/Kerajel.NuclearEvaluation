using System.Data.Common;
using DuckDB.NET.Data;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.RadNet;

public sealed class RadNetSnapshot
{
    readonly string _path;

    public RadNetSnapshot(IConfiguration configuration, IHostEnvironment environment)
        : this(
            Path.GetFullPath(
                configuration["RadNet:SnapshotPath"] ?? "App_Data/radnet.duckdb",
                environment.ContentRootPath
            )
        ) { }

    internal RadNetSnapshot(string path) => _path = path;

    internal async Task<RadNetSummary> SummaryAsync(CancellationToken cancellationToken)
    {
        await using DuckDBConnection? connection = await OpenAsync(cancellationToken);
        if (connection is null)
            return new(false, 0, 0, 0, [], new Dictionary<string, string>());

        await using DuckDBCommand counts = (DuckDBCommand)connection.CreateCommand();
        counts.CommandText = """
            SELECT (SELECT count(*) FROM stations), count(*), coalesce(sum(row_count), 0)::BIGINT
            FROM source_files
            """;
        long stationCount;
        long fileCount;
        long observationCount;
        await using (DbDataReader reader = await counts.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            stationCount = reader.GetInt64(0);
            fileCount = reader.GetInt64(1);
            observationCount = reader.GetInt64(2);
        }

        await using DuckDBCommand years = (DuckDBCommand)connection.CreateCommand();
        years.CommandText = """
            SELECT year, count(DISTINCT station_id), sum(row_count)::BIGINT
            FROM source_files GROUP BY year ORDER BY year
            """;
        List<RadNetYearCoverage> coverage = [];
        await using (DbDataReader reader = await years.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                coverage.Add(new(reader.GetInt32(0), reader.GetInt64(1), reader.GetInt64(2)));
        }

        await using DuckDBCommand metadataQuery = (DuckDBCommand)connection.CreateCommand();
        metadataQuery.CommandText = """SELECT "key", "value" FROM snapshot_metadata""";
        Dictionary<string, string> metadata = [];
        await using (
            DbDataReader reader = await metadataQuery.ExecuteReaderAsync(cancellationToken)
        )
        {
            while (await reader.ReadAsync(cancellationToken))
                metadata[reader.GetString(0)] = reader.GetString(1);
        }
        return new(true, stationCount, fileCount, observationCount, coverage, metadata);
    }

    internal async Task<RadNetQueryResult?> QueryAsync(
        RadNetQuery query,
        CancellationToken cancellationToken
    )
    {
        RadNetQueryPlan plan = RadNetQueryPlan.Compile(query);
        await using DuckDBConnection? connection = await OpenAsync(cancellationToken);
        if (connection is null)
            return null;

        return await ReadQueryAsync(connection, plan, query.Skip, query.Top, cancellationToken);
    }

    internal static async Task<RadNetQueryResult> ReadQueryAsync(
        DuckDBConnection connection,
        RadNetQueryPlan plan,
        int skip,
        int top,
        CancellationToken cancellationToken
    )
    {
        await using DuckDBCommand countCommand = Command(connection, plan.CountSql, plan.Arguments);
        long count = (long)(await countCommand.ExecuteScalarAsync(cancellationToken) ?? 0L);
        if (skip >= count)
            return new([], count);

        await using DuckDBCommand pageCommand = Command(connection, plan.PageSql, plan.Arguments);
        pageCommand.Parameters.Add(new DuckDBParameter("top", top));
        pageCommand.Parameters.Add(new DuckDBParameter("skip", skip));
        await using DbDataReader reader = await pageCommand.ExecuteReaderAsync(cancellationToken);
        List<RadNetRow> items = new(top);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(ReadRow(reader));
        return new(items, count);
    }

    internal async Task<RadNetFileQuality?> FileQualityAsync(
        int sourceFileId,
        CancellationToken cancellationToken
    )
    {
        await using DuckDBConnection? connection = await OpenAsync(cancellationToken);
        if (connection is null)
            return null;

        RadNetRow sourceFile;
        await using DuckDBCommand fileQuery = (DuckDBCommand)connection.CreateCommand();
        fileQuery.CommandText =
            $"SELECT {RadNetQueryPlan.Projection(RadNetEntity.SourceFile)} "
            + RadNetQueryPlan.From(RadNetEntity.SourceFile)
            + " WHERE f.id = $id";
        fileQuery.Parameters.Add(new DuckDBParameter("id", sourceFileId));
        await using (DbDataReader reader = await fileQuery.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            sourceFile = ReadRow(reader);
        }

        string channelCounts = string.Join(
            ", ",
            RadNetQueryPlan.ChannelNames.Select(name => $"count({name.ToLowerInvariant()})")
        );
        await using DuckDBCommand qualityQuery = (DuckDBCommand)connection.CreateCommand();
        qualityQuery.CommandText = $"""
            SELECT count(*), min(observed_at), max(observed_at),
                coalesce(date_diff('hour', min(observed_at), max(observed_at)) + 1
                    - count(DISTINCT date_trunc('hour', observed_at)), 0),
                count(*) FILTER (WHERE dose_nsv_h IS NULL),
                count(*) FILTER (WHERE status IS DISTINCT FROM 'APPROVED'),
                {channelCounts}
            FROM observations WHERE source_file_id = $id
            """;
        qualityQuery.Parameters.Add(new DuckDBParameter("id", sourceFileId));
        await using DbDataReader quality = await qualityQuery.ExecuteReaderAsync(cancellationToken);
        await quality.ReadAsync(cancellationToken);
        return new(
            sourceFile,
            quality.GetInt64(0),
            UtcDate(quality, 1),
            UtcDate(quality, 2),
            quality.GetInt64(3),
            quality.GetInt64(4),
            quality.GetInt64(5),
            RadNetQueryPlan
                .ChannelNames.Select(
                    (name, index) => new RadNetChannelCoverage(name, quality.GetInt64(index + 6))
                )
                .ToArray()
        );
    }

    internal async Task<DuckDBConnection?> OpenAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
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

    internal static DuckDBCommand Command(
        DuckDBConnection connection,
        string sql,
        IReadOnlyDictionary<string, object> arguments
    )
    {
        DuckDBCommand command = (DuckDBCommand)connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in arguments)
            command.Parameters.Add(new DuckDBParameter(name, value));
        return command;
    }

    internal static RadNetRow ReadRow(DbDataReader reader) =>
        new()
        {
            StationId = reader.GetInt32(0),
            StationName = reader.GetString(1),
            State = reader.GetString(2),
            Latitude = NullableValue<double>(reader, 3),
            Longitude = NullableValue<double>(reader, 4),
            SourceFileId = NullableValue<int>(reader, 5),
            Year = NullableValue<int>(reader, 6),
            ArchiveUrl = NullableString(reader, 7),
            ArchiveSha256 = NullableString(reader, 8),
            FileName = NullableString(reader, 9),
            SourceRows = NullableValue<long>(reader, 10),
            InputRows = NullableValue<long>(reader, 11),
            RejectedRows = NullableValue<long>(reader, 12),
            DuplicateRows = NullableValue<long>(reader, 13),
            ObservationId = NullableValue<long>(reader, 14),
            SourceRow = NullableValue<int>(reader, 15),
            ObservedAt = UtcDate(reader, 16),
            Status = NullableString(reader, 17),
            ExposureMrH = NullableValue<double>(reader, 18),
            DoseNsvH = NullableValue<double>(reader, 19),
            Channel = NullableString(reader, 20),
            CountRate = NullableValue<double>(reader, 21),
        };

    static T? NullableValue<T>(DbDataReader reader, int ordinal)
        where T : struct => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<T>(ordinal);

    static string? NullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    static DateTime? UtcDate(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? null
            : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
}
