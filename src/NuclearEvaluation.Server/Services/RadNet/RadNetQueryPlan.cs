using System.Globalization;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.RadNet;

internal sealed record RadNetQueryPlan(
    string CountSql,
    string PageSql,
    IReadOnlyDictionary<string, object> Arguments
)
{
    internal static IReadOnlyList<string> ChannelNames { get; } =
    ["R02", "R03", "R04", "R05", "R06", "R07", "R08", "R09"];

    static readonly string ChannelRelation =
        "(VALUES " + string.Join(", ", ChannelNames.Select(name => $"('{name}')")) + ") c(channel)";

    static readonly string ChannelCount =
        "CASE c.channel "
        + string.Join(
            " ",
            ChannelNames.Select(name => $"WHEN '{name}' THEN o.{name.ToLowerInvariant()}")
        )
        + " END";

    internal static RadNetQueryPlan Compile(RadNetQuery request) =>
        Compile(request, "", From(request.Entity), []);

    // The caller can supply a trusted relation with writable query parameters for its scope.
    internal static RadNetQueryPlan Compile(
        RadNetQuery request,
        string prefix,
        string from,
        Dictionary<string, object> arguments
    )
    {
        if (!Enum.IsDefined(request.Entity))
            throw new ArgumentException("Choose a valid result entity.");
        if (request.Skip < 0 || request.Top is < 1 or > RadNetCatalog.MaxPageSize)
            throw new ArgumentException(
                $"Pages must contain 1 to {RadNetCatalog.MaxPageSize} rows."
            );

        IReadOnlyList<RadNetFilterGroup> groups = request.Filters ?? [];
        if (groups.Count > RadNetCatalog.MaxGroups)
            throw new ArgumentException(
                $"Use no more than {RadNetCatalog.MaxGroups} filter groups."
            );

        Dictionary<RadNetEntity, List<string>> filters = [];
        int conditionCount = 0;
        foreach (RadNetFilterGroup group in groups)
        {
            if (group is null || !Enum.IsDefined(group.Entity) || group.Conditions is null)
                throw new ArgumentException(
                    "Every filter group must name an entity and its conditions."
                );
            if (group.Conditions.Count == 0)
                continue;

            conditionCount += group.Conditions.Count;
            if (conditionCount > RadNetCatalog.MaxConditions)
                throw new ArgumentException(
                    $"Use no more than {RadNetCatalog.MaxConditions} conditions."
                );

            string groupWhere = string.Join(
                group.MatchAny ? " OR " : " AND ",
                group.Conditions.Select(condition =>
                    ConditionWhere(group.Entity, condition, arguments)
                )
            );
            if (!filters.TryGetValue(group.Entity, out List<string>? entityFilters))
                filters.Add(group.Entity, entityFilters = []);
            entityFilters.Add($"({groupWhere})");
        }

        RadNetEntity entity = request.Entity;
        List<string> where = filters
            .Where(pair => pair.Key <= entity)
            .SelectMany(pair => pair.Value)
            .ToList();
        if (entity == RadNetEntity.Channel)
            where.Add($"{ChannelCount} IS NOT NULL");

        if (filters.Keys.Any(scope => scope > entity))
            where.Add(DescendantWhere(entity + 1, filters.Keys.Max(), filters));

        string filteredFrom =
            from + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : "");
        string sort = Sort(request);
        string sqlPrefix = prefix.Length == 0 ? "" : prefix + "\n";
        return new(
            $"{sqlPrefix}SELECT count(*) {filteredFrom}",
            $"{sqlPrefix}SELECT {Projection(entity)} {filteredFrom} ORDER BY {sort} LIMIT $top OFFSET $skip",
            arguments
        );
    }

    internal static string Projection(RadNetEntity entity)
    {
        string station = "s.id, s.name, s.state, s.latitude, s.longitude";
        string file =
            entity >= RadNetEntity.SourceFile
                ? "f.id, f.year, f.archive_url, f.archive_sha256, f.file_name, f.row_count, f.source_rows, f.rejected_rows, f.duplicate_rows"
                : "NULL::INTEGER, NULL::INTEGER, NULL::VARCHAR, NULL::VARCHAR, NULL::VARCHAR, NULL::BIGINT, NULL::BIGINT, NULL::BIGINT, NULL::BIGINT";
        string observation =
            entity >= RadNetEntity.Observation
                ? "o.id, o.source_row, o.observed_at, o.status, o.exposure_mr_h, o.dose_nsv_h"
                : "NULL::BIGINT, NULL::INTEGER, NULL::TIMESTAMP, NULL::VARCHAR, NULL::DOUBLE, NULL::DOUBLE";
        string channel =
            entity == RadNetEntity.Channel
                ? $"c.channel, {ChannelCount}"
                : "NULL::VARCHAR, NULL::DOUBLE";
        return $"{station}, {file}, {observation}, {channel}";
    }

    internal static string From(RadNetEntity entity)
    {
        string from = "FROM stations s";
        if (entity >= RadNetEntity.SourceFile)
            from += " JOIN source_files f ON f.station_id = s.id";
        if (entity >= RadNetEntity.Observation)
            from += " JOIN observations o ON o.source_file_id = f.id";
        if (entity == RadNetEntity.Channel)
            from += " CROSS JOIN " + ChannelRelation;
        return from;
    }

    // A parent qualifies through a single descendant branch. Checking each entity in an
    // independent EXISTS would allow predicates to match different files or observations.
    static string DescendantWhere(
        RadNetEntity entity,
        RadNetEntity last,
        Dictionary<RadNetEntity, List<string>> filters
    )
    {
        if (entity == RadNetEntity.Channel)
        {
            // The child is projected from eight nullable columns. Testing these directly avoids
            // expanding each candidate observation when the result is a parent entity.
            string channelFilters = string.Join(" AND ", filters[entity]);
            return "("
                + string.Join(
                    " OR ",
                    ChannelNames.Select(name =>
                    {
                        string column = "o." + name.ToLowerInvariant();
                        string predicate = channelFilters
                            .Replace(ChannelCount, column, StringComparison.Ordinal)
                            .Replace("c.channel", $"'{name}'", StringComparison.Ordinal);
                        return $"({column} IS NOT NULL AND {predicate})";
                    })
                )
                + ")";
        }

        (string table, string relation) =
            entity == RadNetEntity.SourceFile
                ? ("source_files f", "f.station_id = s.id")
                : ("observations o", "o.source_file_id = f.id");
        List<string> predicates = [relation];
        if (filters.TryGetValue(entity, out List<string>? entityFilters))
            predicates.AddRange(entityFilters);
        if (entity < last)
            predicates.Add(DescendantWhere(entity + 1, last, filters));

        return $"EXISTS (SELECT 1 FROM {table} WHERE {string.Join(" AND ", predicates)})";
    }

    static string ConditionWhere(
        RadNetEntity entity,
        RadNetCondition condition,
        Dictionary<string, object> arguments
    )
    {
        if (
            condition is null
            || FindField(condition.Field) is not { } field
            || field.Entity != entity
        )
            throw new ArgumentException("Choose a valid field for the filter group.");
        if (!RadNetCatalog.CanCompare(field, condition.Operator))
            throw new ArgumentException($"The comparison is not available for {field.Label}.");

        string column = Column(field);
        if (condition.Operator == RadNetOperator.IsPresent)
            return column + " IS NOT NULL";
        if (condition.Operator == RadNetOperator.IsMissing)
            return column + " IS NULL";

        string parameterName = "p" + arguments.Count.ToString(CultureInfo.InvariantCulture);
        arguments.Add(parameterName, ReadValue(field, condition.Value));
        string parameter = "$" + parameterName;
        if (field.Type == RadNetFieldType.Text)
        {
            column = $"lower({column})";
            parameter = $"lower({parameter})";
        }
        if (condition.Operator == RadNetOperator.Contains)
            return $"contains({column}, {parameter})";

        string comparison = condition.Operator switch
        {
            RadNetOperator.Equal => "=",
            RadNetOperator.NotEqual => "<>",
            RadNetOperator.GreaterThan => ">",
            RadNetOperator.AtLeast => ">=",
            RadNetOperator.LessThan => "<",
            RadNetOperator.AtMost => "<=",
            _ => throw new ArgumentException("Choose a valid comparison."),
        };
        return $"{column} {comparison} {parameter}";
    }

    static object ReadValue(RadNetField field, string? value)
    {
        if (value is null || value.Length > 200)
            throw new ArgumentException(
                $"Enter a value of at most 200 characters for {field.Label}."
            );
        return field.Type switch
        {
            RadNetFieldType.Text => value,
            RadNetFieldType.Integer
                when long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long integer
                ) => integer,
            RadNetFieldType.Number
                when double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double number
                ) && double.IsFinite(number) => number,
            RadNetFieldType.DateTime
                when value is { Length: >= 10 }
                    && value[4] == '-'
                    && value[7] == '-'
                    && DateTimeOffset.TryParse(
                        value,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset date
                    ) => date.UtcDateTime,
            _ => throw new ArgumentException($"Enter a valid value for {field.Label}."),
        };
    }

    static string Sort(RadNetQuery request)
    {
        string tieBreaker = request.Entity switch
        {
            RadNetEntity.Station => "s.id",
            RadNetEntity.SourceFile => "f.id",
            RadNetEntity.Observation => "o.id",
            _ => "o.id, c.channel",
        };

        if (string.IsNullOrWhiteSpace(request.SortField))
        {
            return request.Entity switch
            {
                RadNetEntity.Station => "s.name, s.id",
                RadNetEntity.SourceFile => "f.year DESC, s.name, f.id",
                _ => "o.observed_at DESC, " + tieBreaker,
            };
        }

        if (FindField(request.SortField) is not { } field || field.Entity > request.Entity)
            throw new ArgumentException("Choose a sort field available on the result entity.");
        return $"{Column(field)} {(request.Descending ? "DESC" : "ASC")} NULLS LAST, {tieBreaker}";
    }

    static RadNetField? FindField(string? name) =>
        RadNetCatalog.Fields.FirstOrDefault(field =>
            string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)
        );

    static string Column(RadNetField field) =>
        field.Name switch
        {
            nameof(RadNetRow.StationId) => "s.id",
            nameof(RadNetRow.StationName) => "s.name",
            nameof(RadNetRow.State) => "s.state",
            nameof(RadNetRow.SourceFileId) => "f.id",
            nameof(RadNetRow.Year) => "f.year",
            nameof(RadNetRow.FileName) => "f.file_name",
            nameof(RadNetRow.SourceRows) => "f.row_count",
            nameof(RadNetRow.ObservationId) => "o.id",
            nameof(RadNetRow.SourceRow) => "o.source_row",
            nameof(RadNetRow.ObservedAt) => "o.observed_at",
            nameof(RadNetRow.Status) => "o.status",
            nameof(RadNetRow.ExposureMrH) => "o.exposure_mr_h",
            nameof(RadNetRow.DoseNsvH) => "o.dose_nsv_h",
            nameof(RadNetRow.Channel) => "c.channel",
            nameof(RadNetRow.CountRate) => ChannelCount,
            _ => throw new InvalidOperationException("The RadNet field has no SQL mapping."),
        };
}
