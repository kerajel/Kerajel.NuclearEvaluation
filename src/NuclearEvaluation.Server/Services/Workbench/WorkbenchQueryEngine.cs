using System.Data.Common;
using System.Text.Json;
using DuckDB.NET.Data;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Workbench;

public sealed class WorkbenchQueryEngine(RadNetSnapshot snapshot)
{
    const string Scope = """
        WITH ranges AS (
            SELECT CAST(value ->> 'SeriesId' AS UUID) AS series_id,
                value ->> 'State' AS state, value ->> 'Station' AS station,
                CAST(value ->> 'From' AS DATE) AS starts,
                CAST(value ->> 'To' AS DATE) + INTERVAL 1 DAY AS until_day,
                year(CAST(value ->> 'To' AS DATE)) AS last_year
            FROM json_each($ranges)
        ),
        archive_scope AS MATERIALIZED (
            SELECT r.series_id, f.id AS file_id, r.starts, r.until_day
            FROM ranges r
            JOIN stations s ON s.state = r.state AND s.name = r.station
            JOIN source_files f ON f.station_id = s.id
                AND f.year BETWEEN year(r.starts) AND r.last_year
            WHERE f.row_count > 0
        """;

    const string ObservationsInScope = """
        FROM observations o JOIN archive_scope scoped
            ON o.source_file_id = scoped.file_id
            AND o.observed_at >= scoped.starts AND o.observed_at < scoped.until_day
        """;

    const string FileDetails = """
        JOIN source_files f ON f.id = o.source_file_id
        JOIN stations s ON s.id = f.station_id
        """;

    const string FileProjection = "f.id, f.year, f.file_name, s.state, s.name";

    const string ObservationProjection =
        "o.id, o.observed_at, o.status, o.exposure_mr_h, o.dose_nsv_h, o.source_row";

    internal async Task<WorkbenchQueryPage> QueryAsync(
        WorkbenchQuery query,
        WorkbenchQuerySource source,
        CancellationToken ct
    )
    {
        WorkbenchQueryPlan plan = WorkbenchQueryPlan.Compile(query);
        WorkbenchQueryRow[] projects = source
            .Projects.Select(Row)
            .Where(row => plan.Matches(WorkbenchQueryEntity.Project, row))
            .ToArray();

        if (
            query.Entity == WorkbenchQueryEntity.Project
            && !plan.HasFilter(WorkbenchQueryEntity.Series)
            && !plan.NeedsObservationCounts
        )
            return PageMetadata(plan, projects);

        bool restrictToProjects =
            query.Entity == WorkbenchQueryEntity.Project
            || plan.HasFilter(WorkbenchQueryEntity.Project);
        WorkbenchQueryMembership[] membership = [];
        HashSet<Guid> projectSeries = [];
        if (restrictToProjects)
        {
            HashSet<Guid> projectIds = projects.Select(row => row.ProjectId!.Value).ToHashSet();
            membership = source
                .Memberships.Where(link => projectIds.Contains(link.ProjectId))
                .ToArray();
            projectSeries = membership.Select(link => link.SeriesId).ToHashSet();
        }

        WorkbenchSeries[] candidates = source
            .Series.Where(series =>
                (!restrictToProjects || projectSeries.Contains(series.Id))
                && plan.Matches(WorkbenchQueryEntity.Series, Row(series))
            )
            .ToArray();

        if (candidates.Length == 0)
            return new([], 0);

        if (query.Entity <= WorkbenchQueryEntity.Series && !plan.NeedsObservationCounts)
            return PageMetadata(
                plan,
                MetadataResults(plan, projects, candidates, membership, null)
            );

        string[] channels = RadNetQueryPlan.ChannelNames.Where(plan.MayMatchChannel).ToArray();
        if (channels.Length == 0)
            return new([], 0);

        await using DuckDBConnection connection = await snapshot.OpenAsync(ct)
            ?? throw new InvalidOperationException("The RadNet archive is not installed.");
        ArchiveFilters filters = BuildArchiveFilters(plan, channels);
        Dictionary<Guid, long>? counts = null;
        if (
            query.Entity <= WorkbenchQueryEntity.Series
            || query.MinimumMatchingObservations.HasValue
        )
        {
            counts = await ReadCountsAsync(connection, filters, candidates, ct);
            int minimum = query.MinimumMatchingObservations ?? 1;
            candidates = candidates
                .Where(row => counts.GetValueOrDefault(row.Id) >= minimum)
                .ToArray();
        }

        if (query.Entity <= WorkbenchQueryEntity.Series)
            return PageMetadata(
                plan,
                MetadataResults(plan, projects, candidates, membership, counts)
            );
        if (candidates.Length == 0)
            return new([], 0);

        // Apply the grid filters only after the builder has qualified the Series.
        if (plan.HasResultFilters)
        {
            if (query.Entity == WorkbenchQueryEntity.Channel)
                channels = channels.Where(plan.MayMatchResultChannel).ToArray();
            if (channels.Length == 0)
                return new([], 0);
            filters = BuildArchiveFilters(plan, channels, applyResultFilters: true);
        }

        return await ReadArchivePageAsync(connection, plan, filters, candidates, ct);
    }

    static IEnumerable<WorkbenchQueryRow> MetadataResults(
        WorkbenchQueryPlan plan,
        IReadOnlyList<WorkbenchQueryRow> projects,
        IReadOnlyList<WorkbenchSeries> series,
        IReadOnlyList<WorkbenchQueryMembership> memberships,
        IReadOnlyDictionary<Guid, long>? counts
    )
    {
        if (plan.Query.Entity == WorkbenchQueryEntity.Series)
            return series.Select(row =>
                Row(row) with
                {
                    MatchingObservations = counts?.GetValueOrDefault(row.Id)
                        ?? (plan.RequestsMatchingObservations ? row.ObservationCount : null),
                }
            );

        HashSet<Guid> qualifiedSeries = series.Select(row => row.Id).ToHashSet();
        HashSet<Guid> qualifiedProjects = memberships
            .Where(link => qualifiedSeries.Contains(link.SeriesId))
            .Select(link => link.ProjectId)
            .ToHashSet();
        return projects.Where(project => qualifiedProjects.Contains(project.ProjectId!.Value));
    }

    static WorkbenchQueryPage PageMetadata(
        WorkbenchQueryPlan plan,
        IEnumerable<WorkbenchQueryRow> rows
    )
    {
        WorkbenchQueryRow[] items = rows.Where(plan.MatchesResult).ToArray();
        string sort = plan.SortField;
        bool descending = plan.Descending;
        return new(
            items
                .OrderBy(
                    row => WorkbenchQueryPlan.MetadataValue(row, sort),
                    Comparer<object?>.Create(
                        (left, right) =>
                            left is null ? (right is null ? 0 : 1)
                            : right is null ? -1
                            : descending ? WorkbenchQueryPlan.Compare(right, left)
                            : WorkbenchQueryPlan.Compare(left, right)
                    )
                )
                .ThenBy(row => row.ProjectId ?? row.SeriesId)
                .Skip(plan.Query.Skip)
                .Take(plan.Query.Top)
                .ToArray(),
            items.LongLength
        );
    }

    static WorkbenchQueryRow Row(WorkbenchProject project) =>
        new()
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            ProjectSeriesCount = project.SeriesCount,
        };

    static WorkbenchQueryRow Row(WorkbenchSeries series) =>
        new()
        {
            SeriesId = series.Id,
            SeriesName = series.Name,
            State = series.State,
            Station = series.Station,
            From = series.From,
            To = series.To,
            Review = series.Review,
            Notes = series.Notes,
            SeriesObservationCount = series.ObservationCount,
        };

    static async Task<Dictionary<Guid, long>> ReadCountsAsync(
        DuckDBConnection connection,
        ArchiveFilters filters,
        IReadOnlyList<WorkbenchSeries> candidates,
        CancellationToken ct
    )
    {
        // Each observation can match numerous channel predicates, but can appear at most once
        // in an individual Series. Probe channel columns without joining them for these counts.
        string query = $"""
            {filters.ScopeSql}
            SELECT scoped.series_id, count(*)
            {ObservationsInScope}
            {Where(filters.ObservationWhere, filters.ChannelExists)}
            GROUP BY scoped.series_id
            """;
        await using DuckDBCommand command = FilteredCommand(
            connection,
            query,
            filters,
            candidates.Select(Range)
        );
        await using DbDataReader reader = await command.ExecuteReaderAsync(ct);
        Dictionary<Guid, long> counts = [];
        while (await reader.ReadAsync(ct))
            counts.Add(reader.GetGuid(0), reader.GetInt64(1));
        return counts;
    }

    static async Task<WorkbenchQueryPage> ReadArchivePageAsync(
        DuckDBConnection connection,
        WorkbenchQueryPlan plan,
        ArchiveFilters filters,
        IReadOnlyList<WorkbenchSeries> candidates,
        CancellationToken ct
    )
    {
        // Lower-level records have shared source identities. Merge qualifying Series ranges
        // before following them to the archive rather than deduplicating a large result set.
        SeriesRange[] ranges = WorkbenchArchive.MergeRanges(candidates).Select(Range).ToArray();
        WorkbenchQueryEntity entity = plan.Query.Entity;
        if (entity == WorkbenchQueryEntity.File)
            return await ReadFilesAsync(connection, plan, filters, ranges, ct);

        bool showChannels = entity == WorkbenchQueryEntity.Channel;
        string countExpression = showChannels ? filters.ChannelCount : "count(*)";
        string countFileDetails = plan.ResultFiltersNeedFileDetails ? FileDetails : "";
        string where = Where(filters.ObservationWhere, showChannels ? "" : filters.ChannelExists);
        string countSql = $"""
            {filters.ScopeSql}
            SELECT {countExpression} {ObservationsInScope} {countFileDetails} {where}
            """;
        long total = await CountAsync(connection, countSql, filters, ranges, ct);
        if (plan.Query.Skip >= total)
            return new([], total);

        string entityProjection = showChannels
            ? $"{ObservationProjection}, gamma.channel, {filters.ChannelValue} AS count_rate"
            : ObservationProjection;
        string channelJoin = showChannels ? filters.ChannelJoin : "";
        string channelWhere = showChannels ? filters.SelectedChannelWhere : filters.ChannelExists;
        string sort = WorkbenchQueryPlan
            .ArchiveColumn(plan.SortField)
            .Replace("gamma.count_rate", "count_rate", StringComparison.Ordinal);
        string order = Order(plan, sort, showChannels ? "o.id, gamma.channel" : "o.id");
        string pageSql = $"""
            {filters.ScopeSql}
            SELECT {FileProjection}, {entityProjection}
            {ObservationsInScope}
            {FileDetails}
            {channelJoin}
            {Where(filters.ObservationWhere, channelWhere)}
            ORDER BY {order} LIMIT $top OFFSET $skip
            """;
        return await ReadPageAsync(connection, pageSql, plan, filters, ranges, total, ct);
    }

    static async Task<WorkbenchQueryPage> ReadFilesAsync(
        DuckDBConnection connection,
        WorkbenchQueryPlan plan,
        ArchiveFilters filters,
        IReadOnlyList<SeriesRange> ranges,
        CancellationToken ct
    )
    {
        string source =
            $"{ObservationsInScope} {Where(filters.ObservationWhere, filters.ChannelExists)}";
        string countSql = $"{filters.ScopeSql} SELECT count(DISTINCT o.source_file_id) {source}";
        long total = await CountAsync(connection, countSql, filters, ranges, ct);
        if (plan.Query.Skip >= total)
            return new([], total);

        string order = Order(plan, WorkbenchQueryPlan.ArchiveColumn(plan.SortField), "f.id");
        string pageSql = $"""
            {filters.ScopeSql},
            files AS (SELECT DISTINCT o.source_file_id {source})
            SELECT {FileProjection} FROM files
            JOIN source_files f ON f.id = files.source_file_id
            JOIN stations s ON s.id = f.station_id
            ORDER BY {order} LIMIT $top OFFSET $skip
            """;
        return await ReadPageAsync(connection, pageSql, plan, filters, ranges, total, ct);
    }

    static async Task<long> CountAsync(
        DuckDBConnection connection,
        string sql,
        ArchiveFilters filters,
        IReadOnlyList<SeriesRange> ranges,
        CancellationToken ct
    )
    {
        await using DuckDBCommand command = FilteredCommand(connection, sql, filters, ranges);
        return (long)(await command.ExecuteScalarAsync(ct) ?? 0L);
    }

    static async Task<WorkbenchQueryPage> ReadPageAsync(
        DuckDBConnection connection,
        string sql,
        WorkbenchQueryPlan plan,
        ArchiveFilters filters,
        IReadOnlyList<SeriesRange> ranges,
        long total,
        CancellationToken ct
    )
    {
        await using DuckDBCommand command = FilteredCommand(connection, sql, filters, ranges);
        command.Parameters.Add(new DuckDBParameter("top", plan.Query.Top));
        command.Parameters.Add(new DuckDBParameter("skip", plan.Query.Skip));
        await using DbDataReader reader = await command.ExecuteReaderAsync(ct);
        List<WorkbenchQueryRow> page = new(plan.Query.Top);
        while (await reader.ReadAsync(ct))
            page.Add(ArchiveRow(reader, plan.Query.Entity));
        return new(page, total);
    }

    static WorkbenchQueryRow ArchiveRow(DbDataReader reader, WorkbenchQueryEntity entity)
    {
        WorkbenchQueryRow file = new()
        {
            FileId = reader.GetInt32(0),
            Year = reader.GetInt32(1),
            FileName = reader.GetString(2),
            State = reader.GetString(3),
            Station = reader.GetString(4),
        };
        if (entity == WorkbenchQueryEntity.File)
            return file;

        return file with
        {
            ObservationId = reader.GetInt64(5),
            ObservedAt = DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
            Status = reader.GetString(7),
            ExposureMrH = reader.IsDBNull(8) ? null : reader.GetDouble(8),
            DoseNsvH = reader.IsDBNull(9) ? null : reader.GetDouble(9),
            SourceRow = reader.GetInt32(10),
            Channel = entity == WorkbenchQueryEntity.Channel ? reader.GetString(11) : null,
            CountRate = entity == WorkbenchQueryEntity.Channel ? reader.GetDouble(12) : null,
        };
    }

    static DuckDBCommand FilteredCommand(
        DuckDBConnection connection,
        string sql,
        ArchiveFilters filters,
        IEnumerable<SeriesRange> ranges
    )
    {
        Dictionary<string, object> args = new(filters.Arguments)
        {
            ["ranges"] = JsonSerializer.Serialize(ranges),
        };
        return RadNetSnapshot.Command(connection, sql, args);
    }

    static ArchiveFilters BuildArchiveFilters(
        WorkbenchQueryPlan plan,
        IReadOnlyList<string> channels,
        bool applyResultFilters = false
    )
    {
        Dictionary<string, object> args = [];
        string file = plan.Where(WorkbenchQueryEntity.File, args);
        string observation = plan.Where(WorkbenchQueryEntity.Observation, args);
        string gamma = plan.Where(WorkbenchQueryEntity.Channel, args);
        if (applyResultFilters)
        {
            string result = plan.ResultWhere(args);
            switch (plan.Query.Entity)
            {
                case WorkbenchQueryEntity.File:
                    file = CombineConditions(file, result);
                    break;
                case WorkbenchQueryEntity.Observation:
                    observation = CombineConditions(observation, result);
                    break;
                case WorkbenchQueryEntity.Channel:
                    gamma = CombineConditions(gamma, result);
                    break;
            }
        }
        string[] channelPredicates = channels
            .Select(channel =>
            {
                string reading = "o." + channel.ToLowerInvariant();
                string condition = gamma
                    .Replace("gamma.channel", $"'{channel}'", StringComparison.Ordinal)
                    .Replace("gamma.count_rate", reading, StringComparison.Ordinal);
                return $"{reading} IS NOT NULL{And(condition)}";
            })
            .ToArray();
        string exists =
            gamma.Length == 0
                ? ""
                : "(" + string.Join(" OR ", channelPredicates.Select(test => $"({test})")) + ")";
        string count =
            "coalesce(sum("
            + string.Join(
                " + ",
                channelPredicates.Select(test => $"(CASE WHEN {test} THEN 1 ELSE 0 END)")
            )
            + "), 0)::BIGINT";
        string expand =
            "CROSS JOIN (VALUES "
            + string.Join(", ", channels.Select(channel => $"('{channel}')"))
            + ") gamma(channel)";
        string value =
            "CASE gamma.channel "
            + string.Join(
                " ",
                channels.Select(channel => $"WHEN '{channel}' THEN o.{channel.ToLowerInvariant()}")
            )
            + " END";
        string channelWhere =
            $"{value} IS NOT NULL{And(gamma.Replace("gamma.count_rate", value, StringComparison.Ordinal))}";
        return new(
            $"{Scope}{And(file)})",
            observation,
            exists,
            count,
            expand,
            value,
            channelWhere,
            args
        );
    }

    static string Where(string left, string right) =>
        CombineConditions(left, right) is { Length: > 0 } where ? "WHERE " + where : "";

    static string CombineConditions(string left, string right) =>
        left.Length > 0 ? left + And(right) : right;

    static string And(string condition) => condition.Length > 0 ? " AND " + condition : "";

    static string Order(WorkbenchQueryPlan plan, string sort, string tieBreaker) =>
        $"{sort} {(plan.Descending ? "DESC" : "ASC")} NULLS LAST, {tieBreaker}";

    static SeriesRange Range(WorkbenchSeries row) =>
        new(row.Id, row.State, row.Station, row.From, row.To);

    sealed record SeriesRange(
        Guid SeriesId,
        string State,
        string Station,
        DateOnly From,
        DateOnly To
    );

    sealed record ArchiveFilters(
        string ScopeSql,
        string ObservationWhere,
        string ChannelExists,
        string ChannelCount,
        string ChannelJoin,
        string ChannelValue,
        string SelectedChannelWhere,
        Dictionary<string, object> Arguments
    );
}
