using System.Globalization;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Workbench;

internal sealed class WorkbenchQueryPlan
{
    readonly IReadOnlyList<FilterGroup> _groups;
    readonly IReadOnlyList<FilterGroup> _resultGroups;

    WorkbenchQueryPlan(
        WorkbenchQuery query,
        IReadOnlyList<FilterGroup> groups,
        IReadOnlyList<FilterGroup> resultGroups
    )
    {
        Query = query;
        _groups = groups;
        _resultGroups = resultGroups;
    }

    internal WorkbenchQuery Query { get; }

    internal bool HasFilter(WorkbenchQueryEntity entity) =>
        _groups.Any(group => group.Entity == entity);

    internal bool HasArchiveFilters =>
        _groups.Any(group => group.Entity >= WorkbenchQueryEntity.File);

    internal bool NeedsObservationCounts =>
        HasArchiveFilters || Query.MinimumMatchingObservations.HasValue;

    internal bool HasResultFilters => _resultGroups.Count > 0;

    IEnumerable<Filter> ResultConditions => _resultGroups.SelectMany(group => group.Conditions);

    internal bool ResultFiltersNeedFileDetails =>
        Query.Entity is WorkbenchQueryEntity.Observation or WorkbenchQueryEntity.Channel
        && ResultConditions.Any(filter =>
            ArchiveColumn(filter.Field.Name)
                is "s.name"
                    or "s.state"
                    or "f.file_name"
                    or "f.id"
                    or "f.year"
        );

    internal bool RequestsMatchingObservations =>
        SortField == nameof(WorkbenchQueryRow.MatchingObservations)
        || ResultConditions.Any(filter =>
            filter.Field.Name == nameof(WorkbenchQueryRow.MatchingObservations)
        );

    internal static WorkbenchQueryPlan Compile(WorkbenchQuery query)
    {
        if (!Enum.IsDefined(query.Entity))
            throw new ArgumentException("Choose a valid result level.");
        if (query.Skip < 0 || query.Top is < 1 or > WorkbenchQueryCatalog.MaxPageSize)
            throw new ArgumentException(
                $"Choose between 1 and {WorkbenchQueryCatalog.MaxPageSize} rows per page."
            );
        if (query.MinimumMatchingObservations is < 1)
            throw new ArgumentException(
                "The minimum number of matching observations must be at least one."
            );
        if (query.SortField is not null && FindResultField(query.Entity, query.SortField) is null)
            throw new ArgumentException("Choose a sort field on the selected result level.");

        IReadOnlyList<WorkbenchQueryFilterGroup> groups = query.Filters ?? [];
        if (groups.Count > WorkbenchQueryCatalog.MaxGroups)
            throw new ArgumentException(
                $"Use at most {WorkbenchQueryCatalog.MaxGroups} filter groups."
            );

        int count = 0;
        List<FilterGroup> parsedGroups = [];
        foreach (WorkbenchQueryFilterGroup group in groups)
        {
            if (group is null || !Enum.IsDefined(group.Entity) || group.Conditions is null)
                throw new ArgumentException("Choose a level and conditions for each filter group.");
            count += group.Conditions.Count;
            if (count > WorkbenchQueryCatalog.MaxConditions)
                throw new ArgumentException(
                    $"Use at most {WorkbenchQueryCatalog.MaxConditions} conditions."
                );
            if (group.Conditions.Count != 0)
            {
                parsedGroups.Add(
                    new(
                        group.Entity,
                        group.MatchAny,
                        group
                            .Conditions.Select(condition => Parse(group.Entity, condition))
                            .ToArray()
                    )
                );
            }
        }
        IReadOnlyList<WorkbenchQueryCondition> resultFilters = query.ResultFilters ?? [];
        if (resultFilters.Count > WorkbenchQueryCatalog.MaxConditions)
            throw new ArgumentException(
                $"Use at most {WorkbenchQueryCatalog.MaxConditions} result filters."
            );

        List<FilterGroup> parsedResultGroups = [];
        if (resultFilters.Count > 0)
        {
            parsedResultGroups.Add(
                new(
                    query.Entity,
                    false,
                    resultFilters
                        .Select(condition => Parse(query.Entity, condition, resultFilter: true))
                        .ToArray()
                )
            );
        }
        int resultConditionCount = resultFilters.Count;
        foreach (ColumnFilterGroup group in query.ResultFilterGroups ?? [])
        {
            if (group?.Conditions is not { Count: >= 1 and <= ColumnFilterGroup.MaxConditions })
                throw new ArgumentException("Use one or two conditions for each result column.");

            resultConditionCount += group.Conditions.Count;
            if (resultConditionCount > WorkbenchQueryCatalog.MaxConditions)
                throw new ArgumentException(
                    $"Use at most {WorkbenchQueryCatalog.MaxConditions} result filters."
                );
            parsedResultGroups.Add(
                new(
                    query.Entity,
                    group.MatchAny,
                    group
                        .Conditions.Select(condition =>
                        {
                            if (condition is null)
                                throw new ArgumentException(
                                    "Choose a comparison for the result column."
                                );
                            return Parse(
                                query.Entity,
                                new(group.Field, condition.Operator, condition.Value),
                                resultFilter: true
                            );
                        })
                        .ToArray()
                )
            );
        }
        return new(query, parsedGroups, parsedResultGroups);
    }

    internal bool Matches(WorkbenchQueryEntity entity, WorkbenchQueryRow row) =>
        _groups.Where(group => group.Entity == entity).All(group => group.Matches(row));

    internal bool MayMatchChannel(string channel) =>
        _groups
            .Where(group => group.Entity == WorkbenchQueryEntity.Channel)
            .All(group => group.MayMatchChannel(channel));

    internal bool MatchesResult(WorkbenchQueryRow row) =>
        _resultGroups.All(group => group.Matches(row));

    internal bool MayMatchResultChannel(string channel) =>
        _resultGroups.All(group => group.MayMatchChannel(channel));

    internal string ResultWhere(Dictionary<string, object> args) =>
        string.Join(" AND ", _resultGroups.Select(group => group.ToSql(args)));

    internal string Where(WorkbenchQueryEntity entity, Dictionary<string, object> args) =>
        string.Join(
            " AND ",
            _groups.Where(group => group.Entity == entity).Select(group => group.ToSql(args))
        );

    internal string SortField =>
        FindResultField(Query.Entity, Query.SortField)?.Name
        ?? (
            Query.Entity switch
            {
                WorkbenchQueryEntity.Project => nameof(WorkbenchQueryRow.ProjectName),
                WorkbenchQueryEntity.Series => nameof(WorkbenchQueryRow.SeriesName),
                WorkbenchQueryEntity.File => nameof(WorkbenchQueryRow.Year),
                _ => nameof(WorkbenchQueryRow.ObservedAt),
            }
        );

    internal bool Descending =>
        Query.SortField is not null ? Query.Descending : Query.Entity >= WorkbenchQueryEntity.File;

    internal static object? MetadataValue(WorkbenchQueryRow row, string field) =>
        field switch
        {
            nameof(WorkbenchQueryRow.ProjectId) => row.ProjectId?.ToString("D"),
            nameof(WorkbenchQueryRow.ProjectName) => row.ProjectName,
            nameof(WorkbenchQueryRow.ProjectSeriesCount) => (long?)row.ProjectSeriesCount,
            nameof(WorkbenchQueryRow.SeriesId) => row.SeriesId?.ToString("D"),
            nameof(WorkbenchQueryRow.SeriesName) => row.SeriesName,
            nameof(WorkbenchQueryRow.State) => row.State,
            nameof(WorkbenchQueryRow.Station) => row.Station,
            nameof(WorkbenchQueryRow.From) => row.From?.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc
            ),
            nameof(WorkbenchQueryRow.To) => row.To?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            nameof(WorkbenchQueryRow.Review) => row.Review?.ToString(),
            nameof(WorkbenchQueryRow.Notes) => row.Notes,
            nameof(WorkbenchQueryRow.SeriesObservationCount) => row.SeriesObservationCount,
            nameof(WorkbenchQueryRow.MatchingObservations) => row.MatchingObservations,
            _ => throw new InvalidOperationException("The field has no workspace mapping."),
        };

    internal static int Compare(object left, object right) =>
        left is string text && right is string other
            ? StringComparer.OrdinalIgnoreCase.Compare(text, other)
            : ((IComparable)left).CompareTo(right);

    internal static string ArchiveColumn(string field) =>
        field switch
        {
            nameof(WorkbenchQueryRow.FileId) => "f.id",
            nameof(WorkbenchQueryRow.FileName) => "f.file_name",
            nameof(WorkbenchQueryRow.Year) => "f.year",
            nameof(WorkbenchQueryRow.ObservationId) => "o.id",
            nameof(WorkbenchQueryRow.SourceRow) => "o.source_row",
            nameof(WorkbenchQueryRow.ObservedAt) => "o.observed_at",
            nameof(WorkbenchQueryRow.Status) => "o.status",
            nameof(WorkbenchQueryRow.ExposureMrH) => "o.exposure_mr_h",
            nameof(WorkbenchQueryRow.DoseNsvH) => "o.dose_nsv_h",
            nameof(WorkbenchQueryRow.Channel) => "gamma.channel",
            nameof(WorkbenchQueryRow.CountRate) => "gamma.count_rate",
            nameof(WorkbenchQueryRow.State) => "s.state",
            nameof(WorkbenchQueryRow.Station) => "s.name",
            _ => throw new InvalidOperationException("The field has no archive mapping."),
        };

    static WorkbenchQueryField? FindField(WorkbenchQueryEntity entity, string? name) =>
        WorkbenchQueryCatalog.Fields.FirstOrDefault(field =>
            field.Entity == entity
            && string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)
        );

    static WorkbenchQueryField? FindResultField(WorkbenchQueryEntity entity, string? name) =>
        WorkbenchQueryCatalog.ResultFields.FirstOrDefault(field =>
            field.Entity == entity
            && string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)
        );

    static Filter Parse(
        WorkbenchQueryEntity entity,
        WorkbenchQueryCondition condition,
        bool resultFilter = false
    )
    {
        WorkbenchQueryField? field = resultFilter
            ? FindResultField(entity, condition?.Field)
            : FindField(entity, condition?.Field);
        if (condition is null || field is null)
            throw new ArgumentException(
                resultFilter
                    ? "Choose a filter field on the selected result level."
                    : "Choose a valid field on the filter's level."
            );
        if (!WorkbenchQueryCatalog.CanCompare(field, condition.Operator))
            throw new ArgumentException($"The comparison is not available for {field.Label}.");

        if (condition.Operator is RadNetOperator.IsPresent or RadNetOperator.IsMissing)
            return new(field, condition.Operator, null);

        string? value = condition.Value;
        if (value is null || value.Length > 200)
            throw new ArgumentException(
                $"Enter a value of at most 200 characters for {field.Label}."
            );
        object typed = field.Type switch
        {
            RadNetFieldType.Text => value,
            RadNetFieldType.Integer
                when long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long number
                ) => number,
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
        return new(field, condition.Operator, typed);
    }

    sealed record FilterGroup(
        WorkbenchQueryEntity Entity,
        bool MatchAny,
        IReadOnlyList<Filter> Conditions
    )
    {
        public bool Matches(WorkbenchQueryRow row) =>
            Evaluate(condition => condition.Matches(MetadataValue(row, condition.Field.Name)));

        public bool MayMatchChannel(string channel) =>
            Evaluate(condition =>
                condition.Field.Name != nameof(WorkbenchQueryRow.Channel)
                || condition.Matches(channel)
            );

        bool Evaluate(Func<Filter, bool> predicate) =>
            MatchAny ? Conditions.Any(predicate) : Conditions.All(predicate);

        public string ToSql(Dictionary<string, object> args) =>
            "("
            + string.Join(
                MatchAny ? " OR " : " AND ",
                Conditions.Select(condition =>
                    condition.ToSql(ArchiveColumn(condition.Field.Name), args)
                )
            )
            + ")";
    }

    sealed record Filter(WorkbenchQueryField Field, RadNetOperator Operator, object? Value)
    {
        public bool Matches(object? value)
        {
            if (Operator == RadNetOperator.IsPresent)
                return value is not null;
            if (Operator == RadNetOperator.IsMissing)
                return value is null;
            if (value is null)
                return false;
            if (Operator == RadNetOperator.Contains)
                return ((string)value).Contains((string)Value!, StringComparison.OrdinalIgnoreCase);

            int comparison = Compare(value, Value!);
            return Operator switch
            {
                RadNetOperator.Equal => comparison == 0,
                RadNetOperator.NotEqual => comparison != 0,
                RadNetOperator.GreaterThan => comparison > 0,
                RadNetOperator.AtLeast => comparison >= 0,
                RadNetOperator.LessThan => comparison < 0,
                RadNetOperator.AtMost => comparison <= 0,
                _ => throw new InvalidOperationException(
                    "The comparison has no workspace mapping."
                ),
            };
        }

        public string ToSql(string column, Dictionary<string, object> args)
        {
            if (Operator == RadNetOperator.IsPresent)
                return column + " IS NOT NULL";
            if (Operator == RadNetOperator.IsMissing)
                return column + " IS NULL";

            string name = "filter" + args.Count.ToString(CultureInfo.InvariantCulture);
            args.Add(name, Value!);
            string parameter = "$" + name;
            if (Field.Type == RadNetFieldType.Text)
            {
                column = $"lower({column})";
                parameter = $"lower({parameter})";
            }
            if (Operator == RadNetOperator.Contains)
                return $"contains({column}, {parameter})";

            string comparison = Operator switch
            {
                RadNetOperator.Equal => "=",
                RadNetOperator.NotEqual => "<>",
                RadNetOperator.GreaterThan => ">",
                RadNetOperator.AtLeast => ">=",
                RadNetOperator.LessThan => "<",
                RadNetOperator.AtMost => "<=",
                _ => throw new InvalidOperationException("The comparison has no archive mapping."),
            };
            return $"{column} {comparison} {parameter}";
        }
    }
}
