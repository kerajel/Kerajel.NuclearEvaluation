using System.Globalization;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.RemDb;

internal sealed record RemDbQueryPlan(
    string CountSql,
    string PageSql,
    IReadOnlyDictionary<string, object> Arguments
)
{
    const string SiteTable = "sites s";
    const string WindowTable = "sampling_windows w";
    const string MeasurementTable = "measurements m JOIN source_files f ON f.id = m.source_file_id";
    const string SiteRelation = "w.site_id = s.id";
    const string WindowRelation = "m.window_id = w.id";

    internal static RemDbQueryPlan Compile(RemDbQuery request)
    {
        if (!Enum.IsDefined(request.Entity))
            throw new ArgumentException("Choose a valid result entity.");
        ValidatePage(request.Skip, request.Top);

        IReadOnlyList<RemDbFilterGroup> groups = request.Filters ?? [];
        if (groups.Count > RemDbCatalog.MaxGroups)
            throw new ArgumentException(
                $"Use no more than {RemDbCatalog.MaxGroups} filter groups."
            );

        Dictionary<string, object> arguments = [];
        Dictionary<RemDbEntity, List<string>> filters = [];
        int conditionCount = 0;
        foreach (RemDbFilterGroup group in groups)
        {
            if (group is null || !Enum.IsDefined(group.Entity) || group.Conditions is null)
                throw new ArgumentException(
                    "Every filter group must name an entity and its conditions."
                );
            if (group.Conditions.Count == 0)
                continue;

            conditionCount += group.Conditions.Count;
            if (conditionCount > RemDbCatalog.MaxConditions)
                throw new ArgumentException(
                    $"Use no more than {RemDbCatalog.MaxConditions} conditions."
                );

            string predicate = string.Join(
                group.MatchAny ? " OR " : " AND ",
                group.Conditions.Select(condition =>
                    ConditionWhere(group.Entity, condition, arguments)
                )
            );
            if (!filters.TryGetValue(group.Entity, out List<string>? entityFilters))
                filters.Add(group.Entity, entityFilters = []);
            entityFilters.Add($"({predicate})");
        }

        RemDbEntity entity = request.Entity;
        List<string> where = filters
            .Where(pair => pair.Key <= entity)
            .SelectMany(pair => pair.Value)
            .ToList();
        if (filters.Keys.Any(level => level > entity))
            where.Add(DescendantWhere(entity + 1, filters.Keys.Max(), filters));
        string from = From(entity);
        if (where.Count > 0)
            from += " WHERE " + string.Join(" AND ", where);

        return new(
            $"SELECT count(*) {from}",
            $"SELECT {Projection(entity)} {from} ORDER BY {Sort(request)} LIMIT $top OFFSET $skip",
            arguments
        );
    }

    internal static void ValidatePage(int skip, int top)
    {
        if (skip < 0 || top is < 1 or > RemDbCatalog.MaxPageSize)
            throw new ArgumentException(
                $"Pages must contain 1 to {RemDbCatalog.MaxPageSize} rows and have a nonnegative offset."
            );
    }

    static string From(RemDbEntity entity)
    {
        string from = $"FROM {SiteTable}";
        if (entity >= RemDbEntity.SamplingWindow)
            from += $" JOIN {WindowTable} ON {SiteRelation}";
        if (entity == RemDbEntity.Measurement)
            from +=
                $" JOIN measurements m ON {WindowRelation} JOIN source_files f ON f.id = m.source_file_id";
        return from;
    }

    static string Projection(RemDbEntity entity)
    {
        const string site = "s.id, s.country, s.name, s.latitude, s.longitude";
        string window =
            entity >= RemDbEntity.SamplingWindow
                ? "w.id, w.medium, w.treatment, w.began_on, w.ended_on, nullif(w.source_begin, ''), nullif(w.source_end, '')"
                : "NULL::BIGINT, NULL::VARCHAR, NULL::VARCHAR, NULL::DATE, NULL::DATE, NULL::VARCHAR, NULL::VARCHAR";
        string measurement =
            entity == RemDbEntity.Measurement
                ? "m.id, m.source_file_id, m.source_row, f.year, f.archive_url, f.archive_sha256, f.file_name, m.laboratory, m.apparatus, m.nuclide, m.activity, m.unit, m.qualifier, m.value_type"
                : "NULL::BIGINT, NULL::INTEGER, NULL::INTEGER, NULL::INTEGER, NULL::VARCHAR, NULL::VARCHAR, NULL::VARCHAR, NULL::VARCHAR, NULL::VARCHAR, NULL::VARCHAR, NULL::DOUBLE, NULL::VARCHAR, NULL::VARCHAR, NULL::VARCHAR";
        return $"{site}, {window}, {measurement}";
    }

    // All related filters must match through the same sampling window and measurement.
    static string DescendantWhere(
        RemDbEntity entity,
        RemDbEntity last,
        IReadOnlyDictionary<RemDbEntity, List<string>> filters
    )
    {
        (string table, string relation) =
            entity == RemDbEntity.SamplingWindow
                ? (WindowTable, SiteRelation)
                : (MeasurementTable, WindowRelation);
        List<string> predicates = [relation];
        if (filters.TryGetValue(entity, out List<string>? entityFilters))
            predicates.AddRange(entityFilters);
        if (entity < last)
            predicates.Add(DescendantWhere(entity + 1, last, filters));

        return $"EXISTS (SELECT 1 FROM {table} WHERE {string.Join(" AND ", predicates)})";
    }

    static string ConditionWhere(
        RemDbEntity entity,
        RemDbCondition condition,
        Dictionary<string, object> arguments
    )
    {
        if (
            condition is null
            || FindField(condition.Field) is not { } field
            || field.Entity != entity
        )
            throw new ArgumentException("Choose a valid field for the filter group.");
        if (!RemDbCatalog.CanCompare(field, condition.Operator))
            throw new ArgumentException($"The comparison is unavailable for {field.Label}.");

        string column = Column(field);
        string presentValue =
            field.Type == RadNetFieldType.Text ? $"nullif(trim({column}), '')" : column;
        if (condition.Operator == RadNetOperator.IsPresent)
            return $"{presentValue} IS NOT NULL";
        if (condition.Operator == RadNetOperator.IsMissing)
            return $"{presentValue} IS NULL";

        string parameterName = "p" + arguments.Count.ToString(CultureInfo.InvariantCulture);
        arguments.Add(parameterName, ReadValue(field, condition.Value));
        string parameter = "$" + parameterName;
        if (field.Type == RadNetFieldType.Text)
        {
            column = $"lower({presentValue})";
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

    static object ReadValue(RemDbField field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
            throw new ArgumentException(
                $"Enter a value of at most 200 characters for {field.Label}."
            );
        return field.Type switch
        {
            RadNetFieldType.Text => value.Trim(),
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
                when DateOnly.TryParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateOnly date
                ) => date,
            _ => throw new ArgumentException($"Enter a valid value for {field.Label}."),
        };
    }

    static string Sort(RemDbQuery request)
    {
        (string tieBreaker, string defaultSort) = request.Entity switch
        {
            RemDbEntity.Site => ("s.id", "s.country, s.name"),
            RemDbEntity.SamplingWindow => ("w.id", "w.began_on DESC NULLS LAST"),
            _ => ("m.id", "f.year DESC, w.began_on DESC NULLS LAST"),
        };
        if (string.IsNullOrWhiteSpace(request.SortField))
            return $"{defaultSort}, {tieBreaker}";
        if (FindField(request.SortField) is not { } field || field.Entity > request.Entity)
            throw new ArgumentException("Choose a sort field available on the result entity.");

        return $"{Column(field)} {(request.Descending ? "DESC" : "ASC")} NULLS LAST, {tieBreaker}";
    }

    static RemDbField? FindField(string? key) =>
        RemDbCatalog.Fields.FirstOrDefault(field =>
            string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase)
        );

    static string Column(RemDbField field) =>
        field.Key switch
        {
            nameof(RemDbRow.SiteId) => "s.id",
            nameof(RemDbRow.Country) => "s.country",
            nameof(RemDbRow.SiteName) => "s.name",
            nameof(RemDbRow.Latitude) => "s.latitude",
            nameof(RemDbRow.Longitude) => "s.longitude",
            nameof(RemDbRow.WindowId) => "w.id",
            nameof(RemDbRow.Medium) => "w.medium",
            nameof(RemDbRow.Treatment) => "w.treatment",
            nameof(RemDbRow.BeganOn) => "w.began_on",
            nameof(RemDbRow.EndedOn) => "w.ended_on",
            nameof(RemDbRow.MeasurementId) => "m.id",
            nameof(RemDbRow.SourceFileId) => "m.source_file_id",
            nameof(RemDbRow.SourceRow) => "m.source_row",
            nameof(RemDbRow.SourceYear) => "f.year",
            nameof(RemDbRow.FileName) => "f.file_name",
            nameof(RemDbRow.Laboratory) => "m.laboratory",
            nameof(RemDbRow.Apparatus) => "m.apparatus",
            nameof(RemDbRow.Nuclide) => "m.nuclide",
            nameof(RemDbRow.Activity) => "m.activity",
            nameof(RemDbRow.Unit) => "m.unit",
            nameof(RemDbRow.Qualifier) => "m.qualifier",
            nameof(RemDbRow.ValueType) => "m.value_type",
            _ => throw new InvalidOperationException("The REMdb field has no SQL mapping."),
        };
}
