using System.Data.Common;
using System.Text.Json;
using DuckDB.NET.Data;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Workbench;

public sealed class WorkbenchArchive(RadNetSnapshot snapshot) : IDisposable
{
    readonly SemaphoreSlim _catalogLock = new(1, 1);
    WorkbenchCatalog? _catalog;

    // Resolve station and year before touching observations. The ranges passed to this query
    // are non-overlapping within each station, so a joined observation only contributes once.
    const string ProjectScope = """
        WITH ranges AS (
            SELECT value ->> 'State' AS state, value ->> 'Station' AS station,
                CAST(value ->> 'From' AS DATE) AS starts, CAST(value ->> 'To' AS DATE) AS ends
            FROM json_each($ranges)
        ),
        project_files AS MATERIALIZED (
            SELECT f.id AS file_id, r.starts, r.ends + INTERVAL 1 DAY AS until_day
            FROM ranges r
            JOIN stations s ON s.state = r.state AND s.name = r.station
            JOIN source_files f ON f.station_id = s.id AND f.year BETWEEN year(r.starts) AND year(r.ends)
        )
        """;

    const string ProjectRows = """
        FROM observations o JOIN project_files pf ON pf.file_id = o.source_file_id
            AND o.observed_at >= pf.starts AND o.observed_at < pf.until_day
        """;

    const string InSeries = """
        WHERE s.state = $state AND s.name = $station AND f.year BETWEEN year(CAST($starts AS DATE))
            AND year(CAST($ends AS DATE)) AND o.observed_at >= CAST($starts AS DATE)
            AND o.observed_at < CAST($ends AS DATE) + INTERVAL 1 DAY
        """;

    public async Task<WorkbenchCatalog> CatalogAsync(CancellationToken ct)
    {
        if (Volatile.Read(ref _catalog) is { } cached)
            return cached;

        await _catalogLock.WaitAsync(ct);
        try
        {
            if (_catalog is not null)
                return _catalog;

            RadNetSummary summary = await snapshot.SummaryAsync(ct);
            await using DuckDBConnection? connection = await snapshot.OpenAsync(ct);
            if (!summary.Available || connection is null)
                return new(summary, []);

            // Compute dates at the archive's year granularity from file metadata. The importer
            // guarantees that each occurrence is in its file's UTC year.
            await using DuckDBCommand command = RadNetSnapshot.Command(
                connection,
                """
                SELECT s.state, s.name, min(f.year), max(f.year), sum(f.row_count)::BIGINT, count(*)
                FROM stations s JOIN source_files f ON f.station_id = s.id
                WHERE f.row_count > 0
                GROUP BY s.state, s.name ORDER BY s.state, s.name
                """,
                new Dictionary<string, object>()
            );
            await using DbDataReader reader = await command.ExecuteReaderAsync(ct);
            List<WorkbenchStation> stations = [];
            while (await reader.ReadAsync(ct))
            {
                stations.Add(
                    new(
                        reader.GetString(0),
                        reader.GetString(1),
                        new(reader.GetInt32(2), 1, 1),
                        new(reader.GetInt32(3), 12, 31),
                        reader.GetInt64(4),
                        reader.GetInt64(5)
                    )
                );
            }

            WorkbenchCatalog catalog = new(summary, stations);
            Volatile.Write(ref _catalog, catalog);
            return catalog;
        }
        finally
        {
            _catalogLock.Release();
        }
    }

    public async Task<long> CountAsync(
        WorkbenchStation station,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    )
    {
        if (from == station.FirstDay && to == station.LastDay)
            return station.ObservationCount;

        await using DuckDBConnection connection = await RequireConnectionAsync(ct);
        await using DuckDBCommand command = RadNetSnapshot.Command(
            connection,
            $"SELECT count(*) {RadNetQueryPlan.From(RadNetEntity.Observation)} {InSeries}",
            SeriesArguments(station.State, station.Name, from, to)
        );
        return (long)(await command.ExecuteScalarAsync(ct) ?? 0L);
    }

    public async Task<WorkbenchFilePage> FilesAsync(
        WorkbenchSeries series,
        int skip,
        int top,
        CancellationToken ct
    )
    {
        await using DuckDBConnection connection = await RequireConnectionAsync(ct);
        await using DuckDBCommand command = RadNetSnapshot.Command(
            connection,
            $"""
            SELECT f.id, f.file_name, f.year, count(*), count(*) OVER () AS file_count
            {RadNetQueryPlan.From(RadNetEntity.Observation)} {InSeries}
            GROUP BY f.id, f.file_name, f.year
            ORDER BY f.year DESC, f.id LIMIT $top OFFSET $skip
            """,
            SeriesArguments(series.State, series.Station, series.From, series.To)
        );
        command.Parameters.Add(new DuckDBParameter("top", top));
        command.Parameters.Add(new DuckDBParameter("skip", skip));
        await using DbDataReader reader = await command.ExecuteReaderAsync(ct);
        List<WorkbenchSeriesFile> files = [];
        long total = 0;
        while (await reader.ReadAsync(ct))
        {
            files.Add(
                new(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt64(3))
            );
            total = reader.GetInt64(4);
        }
        if (files.Count > 0 || skip == 0)
            return new(files, total);

        await using DuckDBCommand count = RadNetSnapshot.Command(
            connection,
            $"SELECT count(DISTINCT f.id) {RadNetQueryPlan.From(RadNetEntity.Observation)} {InSeries}",
            SeriesArguments(series.State, series.Station, series.From, series.To)
        );
        await reader.CloseAsync();
        return new(files, (long)(await count.ExecuteScalarAsync(ct) ?? 0L));
    }

    public async Task<WorkbenchProjectStats> ProjectStatsAsync(
        IReadOnlyList<WorkbenchSeries> series,
        CancellationToken ct
    )
    {
        if (series.Count == 0)
            return new(0, 0, 0, []);

        await using DuckDBConnection connection = await RequireConnectionAsync(ct);
        await using DuckDBCommand command = RadNetSnapshot.Command(
            connection,
            $"""
            {ProjectScope}
            SELECT CAST(date_trunc('month', o.observed_at) AS DATE) AS month, count(*),
                count(o.exposure_mr_h), count(o.r03)
            {ProjectRows}
            GROUP BY month ORDER BY month
            """,
            ProjectArguments(series)
        );
        await using DbDataReader reader = await command.ExecuteReaderAsync(ct);
        List<WorkbenchMonth> months = [];
        long observations = 0;
        long exposure = 0;
        long r03 = 0;
        while (await reader.ReadAsync(ct))
        {
            WorkbenchMonth month = new(
                reader.GetFieldValue<DateOnly>(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3)
            );
            months.Add(month);
            observations += month.ObservationCount;
            exposure += month.WithExposure;
            r03 += month.WithR03;
        }
        return new(observations, exposure, r03, months);
    }

    public async Task<RadNetQueryResult> ProjectObservationsAsync(
        IReadOnlyList<WorkbenchSeries> series,
        int skip,
        int top,
        CancellationToken ct
    )
    {
        if (series.Count == 0)
            return new([], 0);

        await using DuckDBConnection connection = await RequireConnectionAsync(ct);
        IReadOnlyDictionary<string, object> arguments = ProjectArguments(series);
        await using DuckDBCommand count = RadNetSnapshot.Command(
            connection,
            $"{ProjectScope} SELECT count(*) {ProjectRows}",
            arguments
        );
        long total = (long)(await count.ExecuteScalarAsync(ct) ?? 0L);
        if (skip >= total)
            return new([], total);

        await using DuckDBCommand page = RadNetSnapshot.Command(
            connection,
            $"""
            {ProjectScope},
            page AS MATERIALIZED (
                SELECT o.* {ProjectRows} ORDER BY o.observed_at DESC, o.id LIMIT $top OFFSET $skip
            )
            SELECT {RadNetQueryPlan.Projection(RadNetEntity.Observation)}
            FROM page o JOIN source_files f ON f.id = o.source_file_id
                JOIN stations s ON s.id = f.station_id
            ORDER BY o.observed_at DESC, o.id
            """,
            arguments
        );
        page.Parameters.Add(new DuckDBParameter("top", top));
        page.Parameters.Add(new DuckDBParameter("skip", skip));
        await using DbDataReader reader = await page.ExecuteReaderAsync(ct);
        List<RadNetRow> rows = [];
        while (await reader.ReadAsync(ct))
            rows.Add(RadNetSnapshot.ReadRow(reader));
        return new(rows, total);
    }

    public async Task<RadNetQueryResult> ProjectObservationsAsync(
        IReadOnlyList<WorkbenchSeries> series,
        RadNetQuery query,
        CancellationToken ct
    )
    {
        if (query.Entity != RadNetEntity.Observation)
            throw new ArgumentException("Choose observations as the project result.");

        RadNetQueryPlan plan = RadNetQueryPlan.Compile(
            query,
            ProjectScope,
            $"""
            {ProjectRows}
            JOIN source_files f ON f.id = o.source_file_id
            JOIN stations s ON s.id = f.station_id
            """,
            ProjectArguments(series)
        );
        if (series.Count == 0)
            return new([], 0);

        await using DuckDBConnection connection = await RequireConnectionAsync(ct);
        return await RadNetSnapshot.ReadQueryAsync(connection, plan, query.Skip, query.Top, ct);
    }

    async Task<DuckDBConnection> RequireConnectionAsync(CancellationToken ct) =>
        await snapshot.OpenAsync(ct)
        ?? throw new InvalidOperationException("The RadNet archive is not installed.");

    static Dictionary<string, object> SeriesArguments(
        string state,
        string station,
        DateOnly from,
        DateOnly to
    ) =>
        new()
        {
            ["state"] = state,
            ["station"] = station,
            ["starts"] = from,
            ["ends"] = to,
        };

    static Dictionary<string, object> ProjectArguments(IReadOnlyList<WorkbenchSeries> series) =>
        new()
        {
            ["ranges"] = JsonSerializer.Serialize(
                MergeRanges(series)
                    .Select(row => new
                    {
                        row.State,
                        row.Station,
                        row.From,
                        row.To,
                    })
            ),
        };

    internal static IEnumerable<WorkbenchSeries> MergeRanges(IReadOnlyList<WorkbenchSeries> series)
    {
        foreach (
            IGrouping<(string State, string Station), WorkbenchSeries> station in series.GroupBy(
                row => (row.State, row.Station)
            )
        )
        {
            WorkbenchSeries? range = null;
            foreach (WorkbenchSeries next in station.OrderBy(row => row.From))
            {
                if (range is not null && next.From <= range.To)
                {
                    if (next.To > range.To)
                        range = range with { To = next.To };
                    continue;
                }
                if (range is not null)
                    yield return range;
                range = next;
            }
            if (range is not null)
                yield return range;
        }
    }

    public void Dispose()
    {
        _catalogLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
