using System.Globalization;
using System.Text.RegularExpressions;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.Assistant;

internal static partial class LocalSeriesQuestion
{
    public static SeriesGridQuery? Parse(string question, SeriesSchemaDescription schema)
    {
        string[] parts = ConditionsStart().Split(question.Trim().TrimEnd('.', '?', '!'), 2);
        Match request = SeriesRequest().Match(parts[0]);
        if (
            !request.Success
            || (!request.Groups["series"].Success && !request.Groups["rank"].Success)
        )
            return null;

        int? limit = ReadCount(request.Groups["limit"].Value);
        if (request.Groups["limit"].Success && limit is not (>= 1 and <= DataQuery.MaxPageSize))
            return null;

        string conditions = parts.Length == 2 ? parts[1] : string.Empty;
        Match trailingOrder = TrailingOrder().Match(conditions);
        if (trailingOrder.Success)
        {
            if (request.Groups["order"].Success)
                return null;
            conditions = conditions[..trailingOrder.Index];
        }

        string rank = request.Groups["rank"].Value.ToLowerInvariant();
        Match sort = trailingOrder.Success ? trailingOrder : request;
        string orderLabel = sort.Groups["order"].Value;
        if (orderLabel.Length == 0 && rank is ("top" or "bottom"))
            return null;

        SeriesQueryOrder? order = ReadOrder(schema, orderLabel, rank, sort);
        if ((orderLabel.Length > 0 || rank.Length > 0) && order is null)
            return null;

        Group typeLabel = request.Groups["type"];
        SeriesType? seriesType = ReadType(schema, typeLabel.Value);
        if (typeLabel.Success && seriesType is null)
            return null;

        List<SeriesFieldFilter> filters = [];
        List<SeriesRelatedFilter> relatedFilters = [];
        if (parts.Length == 2)
        {
            foreach (string clause in SplitConditions(conditions))
            {
                if (ReadRelatedFilter(schema, clause) is { } related)
                {
                    relatedFilters.Add(related);
                    continue;
                }

                if (
                    ReadFieldFilter(schema, SeriesQueryEntity.Series, clause) is not { } filter
                    || ConflictsWith(filters, filter)
                )
                    return null;

                filters.Add(filter);
            }
        }

        SeriesGridQuery query = new()
        {
            Limit = limit,
            Type = seriesType,
            Filters = filters,
            RelatedFilters = relatedFilters,
            Order = order,
        };

        if (
            query.Type is { } type
            && filters.Any(filter =>
                filter.Field == nameof(SeriesView.SeriesType)
                && filter.Comparison == SeriesComparison.Equal
                && !string.Equals(type.ToString(), filter.Value, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            return null;
        }

        return query;
    }

    static SeriesQueryOrder? ReadOrder(
        SeriesSchemaDescription schema,
        string label,
        string rank,
        Match sort
    )
    {
        if (label.Length == 0)
        {
            string? defaultField = rank switch
            {
                "newest" or "latest" or "oldest" => nameof(SeriesView.CreatedAt),
                "first" => nameof(SeriesView.Id),
                _ => null,
            };
            return
                defaultField is not null
                && FindField(schema, SeriesQueryEntity.Series, defaultField) is { } field
                ? new(field.Name, null, rank is "newest" or "latest")
                : null;
        }

        string direction = sort.Groups["direction"].Value.ToLowerInvariant();
        string preference = sort.Groups["preference"].Value.ToLowerInvariant();
        bool descending =
            direction.Length > 0 ? direction is "descending" or "desc"
            : preference.Length > 0 ? preference is "most" or "highest"
            : rank is "top" or "newest" or "latest" || sort.Groups["preference"].Success;

        if (ReadRelatedRows(schema, label) is { } rows)
            return new(null, rows, descending);

        SeriesFieldDescription? orderField = FindField(schema, SeriesQueryEntity.Series, label);
        return orderField is null ? null : new(orderField.Name, null, descending);
    }

    static SeriesRelatedFilter? ReadRelatedFilter(SeriesSchemaDescription schema, string clause)
    {
        Match match = RelatedCondition().Match(clause);
        SeriesComparison? comparison = SeriesComparison.GreaterOrEqual;
        int? count = 1;
        if (match.Success)
        {
            comparison = ReadComparison(match.Groups["operation"].Value);
            count = ReadCount(match.Groups["count"].Value);
        }
        else
        {
            match = HasRelatedCondition().Match(clause);
        }

        if (
            !match.Success
            || comparison is not { } op
            || count is not int number
            || !schema.CountComparisons.Contains(op.ToString(), StringComparer.OrdinalIgnoreCase)
        )
            return null;

        if (ReadRelatedRows(schema, match.Groups["relation"].Value) is not { } rows)
            return null;

        Group condition = match.Groups["condition"];
        if (condition.Success)
        {
            if (
                ReadFieldFilter(schema, rows.Entity, condition.Value) is not { } filter
                || ConflictsWith(rows.Filters, filter)
            )
                return null;

            rows = rows with { Filters = [.. rows.Filters, filter] };
        }

        return new(rows, op, number);
    }

    static SeriesRelatedRows? ReadRelatedRows(SeriesSchemaDescription schema, string label)
    {
        string name = EntityWhitespace().Replace(label, string.Empty).ToLowerInvariant();
        if (
            name
            is "qcsample"
                or "qcsamples"
                or "qualitycontrolsample"
                or "qualitycontrolsamples"
                or "qcsamplecount"
        )
        {
            SeriesFieldDescription? field = FindField(
                schema,
                SeriesQueryEntity.Samples,
                nameof(SampleView.SampleType)
            );
            return
                field is not null
                && SupportsComparison(field, SeriesComparison.Equal)
                && field.EnumValues.FirstOrDefault(value =>
                    value.Equals(nameof(SampleType.Qc), StringComparison.OrdinalIgnoreCase)
                )
                    is { } qc
                ? new(SeriesQueryEntity.Samples, [new(field.Name, SeriesComparison.Equal, qc)])
                : null;
        }

        if (name.EndsWith("count", StringComparison.Ordinal))
            name = name[..^"count".Length];
        SeriesEntityDescription? description = schema.Entities.FirstOrDefault(entity =>
            entity.Entity.Equals(name, StringComparison.OrdinalIgnoreCase)
            || entity.Entity.TrimEnd('s', 'S').Equals(name, StringComparison.OrdinalIgnoreCase)
        );
        return
            description is not null
            && Enum.TryParse(description.Entity, ignoreCase: true, out SeriesQueryEntity found)
            && Enum.IsDefined(found)
            && found != SeriesQueryEntity.Series
            ? new(found, [])
            : null;
    }

    static SeriesFieldFilter? ReadFieldFilter(
        SeriesSchemaDescription schema,
        SeriesQueryEntity entity,
        string clause
    )
    {
        Match match = NullCondition().Match(clause);
        if (match.Success)
        {
            return CreateFilter(
                schema,
                entity,
                match.Groups["field"].Value,
                match.Groups["not"].Success ? SeriesComparison.IsNotNull : SeriesComparison.IsNull,
                null
            );
        }

        match = ScalarCondition().Match(clause);
        string operation = match.Groups["operation"].Value.ToLowerInvariant();
        if (match.Success && ReadComparison(operation) is { } comparison)
        {
            string label = match.Groups["field"].Value;
            string value = match.Groups["value"].Value.Trim();
            if (value is ['"' or '\'', ..])
            {
                if (value.Length < 2 || value[0] != value[^1])
                    return null;

                value = value[1..^1];
            }
            else if (OrInValue().IsMatch(value))
            {
                return null;
            }

            return CreateFilter(schema, entity, label, comparison, value, operation);
        }

        // Retain the short form "where regular" for the Series type.
        return entity == SeriesQueryEntity.Series && ReadType(schema, clause) is { } type
            ? CreateFilter(
                schema,
                entity,
                nameof(SeriesView.SeriesType),
                SeriesComparison.Equal,
                type.ToString()
            )
            : null;
    }

    static SeriesFieldFilter? CreateFilter(
        SeriesSchemaDescription schema,
        SeriesQueryEntity entity,
        string label,
        SeriesComparison comparison,
        string? value,
        string operation = ""
    )
    {
        SeriesFieldDescription? field = FindField(schema, entity, label);
        if (field is null)
            return null;

        if (value is not null)
        {
            if (
                field.ValueType == nameof(DateTime)
                && operation is ("after" or "on or before")
                && DateOnly.TryParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateOnly day
                )
            )
            {
                if (day == DateOnly.MaxValue)
                    return null;

                comparison =
                    operation == "after"
                        ? SeriesComparison.GreaterOrEqual
                        : SeriesComparison.LessThan;
                value = day.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (field.ValueType == "Enum")
                value = ReadEnumOperand(entity, field, value);
        }

        return SupportsComparison(field, comparison) ? new(field.Name, comparison, value) : null;
    }

    static string ReadEnumOperand(
        SeriesQueryEntity entity,
        SeriesFieldDescription field,
        string value
    )
    {
        // Wire values use enum names. Preserve numeric aliases of the shared query enums.
        string candidate = (entity, field.Name) switch
        {
            (SeriesQueryEntity.Series, nameof(SeriesView.SeriesType))
                when Enum.TryParse(value, true, out SeriesType type) => type.ToString(),
            (SeriesQueryEntity.Samples, nameof(SampleView.SampleType))
                when Enum.TryParse(value, true, out SampleType type) => type.ToString(),
            _ => value,
        };
        return field.EnumValues.FirstOrDefault(name =>
            name.Equals(candidate, StringComparison.OrdinalIgnoreCase)
        ) ?? value;
    }

    static bool SupportsComparison(SeriesFieldDescription field, SeriesComparison comparison) =>
        field.Comparisons.Contains(comparison.ToString(), StringComparer.OrdinalIgnoreCase);

    static SeriesFieldDescription? FindField(
        SeriesSchemaDescription schema,
        SeriesQueryEntity entity,
        string label
    )
    {
        string entityName = entity.ToString();
        IReadOnlyList<SeriesFieldDescription>? fields = schema
            .Entities.FirstOrDefault(description =>
                description.Entity.Equals(entityName, StringComparison.OrdinalIgnoreCase)
            )
            ?.Fields;
        if (fields is null)
            return null;

        string fieldKey = FieldKey(label);
        SeriesFieldDescription? field = fields.FirstOrDefault(description =>
            FieldKey(description.Name).Equals(fieldKey, StringComparison.OrdinalIgnoreCase)
        );
        if (field is not null || entity != SeriesQueryEntity.Series)
            return field;

        string? fieldName = label.Trim().ToLowerInvariant() switch
        {
            "type" => nameof(SeriesView.SeriesType),
            "created" or "creation date" => nameof(SeriesView.CreatedAt),
            "completion date" => nameof(SeriesView.AnalysisCompleteDate),
            "number of samples" or "total samples" => nameof(SeriesView.SampleCount),
            _ => null,
        };
        return fieldName is null
            ? null
            : fields.FirstOrDefault(description =>
                description.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase)
            );
    }

    static string FieldKey(string name) =>
        string.Concat(name.Where(character => character != '_' && !char.IsWhiteSpace(character)));

    static bool ConflictsWith(IEnumerable<SeriesFieldFilter> existing, SeriesFieldFilter filter) =>
        filter.Comparison == SeriesComparison.Equal
        && existing.Any(other =>
            other.Field == filter.Field
            && other.Comparison == SeriesComparison.Equal
            && !string.Equals(other.Value, filter.Value, StringComparison.OrdinalIgnoreCase)
        );

    static IEnumerable<string> SplitConditions(string conditions)
    {
        int start = 0;
        foreach (Match match in ConditionSeparators().Matches(conditions))
        {
            if (!match.Groups["separator"].Success)
                continue;

            yield return conditions[start..match.Index].Trim();
            start = match.Index + match.Length;
        }

        yield return conditions[start..].Trim();
    }

    static SeriesComparison? ReadComparison(string text) =>
        text.ToLowerInvariant() switch
        {
            "is" or "=" or "==" or "equals" or "exactly" => SeriesComparison.Equal,
            "is not" or "!=" or "<>" => SeriesComparison.NotEqual,
            ">" or "more than" or "greater than" or "after" => SeriesComparison.GreaterThan,
            ">=" or "at least" or "on or after" or "since" => SeriesComparison.GreaterOrEqual,
            "<" or "less than" or "fewer than" or "before" => SeriesComparison.LessThan,
            "<=" or "at most" or "on or before" => SeriesComparison.LessOrEqual,
            "contains" => SeriesComparison.Contains,
            "starts with" => SeriesComparison.StartsWith,
            "ends with" => SeriesComparison.EndsWith,
            _ => null,
        };

    static SeriesType? ReadType(SeriesSchemaDescription schema, string type) =>
        Enum.TryParse(type, ignoreCase: true, out SeriesType value)
        && Enum.IsDefined(value)
        && FindField(schema, SeriesQueryEntity.Series, nameof(SeriesView.SeriesType)) is { } field
        && SupportsComparison(field, SeriesComparison.Equal)
        && field.EnumValues.Contains(value.ToString(), StringComparer.OrdinalIgnoreCase)
            ? value
            : null;

    static int? ReadCount(string input) =>
        int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;

    const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    const string Types = "regular|medium|sampling|preemptive";
    const string CountOperators = "at least|at most|more than|less than|fewer than|exactly";
    const string ScalarOperators =
        CountOperators
        + "|greater than|on or after|on or before|since|after|before|contains|starts with|ends with|equals|is not|is";
    const string Direction = @"(?:\s+(?<direction>ascending|descending|asc|desc))?";
    const string OrderBy = @"(?:order(?:ed)?|sort(?:ed)?)\s+by\s+";

    [GeneratedRegex(@"\s+(?:where\s+|with\s+(?=(?:" + CountOperators + @")\b))", Options, 250)]
    private static partial Regex ConditionsStart();

    [GeneratedRegex(
        """ "[^"]*" | '[^']*' | (?<separator>\s+and\s+) """,
        Options | RegexOptions.IgnorePatternWhitespace,
        250
    )]
    private static partial Regex ConditionSeparators();

    [GeneratedRegex(@"\s+" + OrderBy + "(?<order>.+?)" + Direction + "$", Options, 250)]
    private static partial Regex TrailingOrder();

    [GeneratedRegex(
        @"^(?:please\s+)?(?:(?:show(?: me)?|list|find|display|give me|get)\s+)?(?:the\s+|all\s+)?"
            + @"(?:(?<rank>top|bottom|first|latest|newest|oldest)\s+(?<limit>[0-9]+)(?=\s|$)\s*)?"
            + "(?:(?<type>"
            + Types
            + @")\s+)?(?<series>series)?"
            + @"(?:\s*(?:"
            + OrderBy
            + @"|by\s+|with\s+(?:the\s+)?(?<preference>most|fewest|highest|lowest)\s+)"
            + @"(?:the\s+)?(?:number of\s+)?(?<order>.+?)"
            + Direction
            + ")?$",
        Options,
        250
    )]
    private static partial Regex SeriesRequest();

    [GeneratedRegex(
        @"^(?:(?:there (?:is|are)|has|have)\s+)?(?<operation>"
            + CountOperators
            + @")\s+(?<count>[0-9]+)\s+(?<relation>.+?)(?:\s+with\s+(?<condition>.+))?$",
        Options,
        250
    )]
    private static partial Regex RelatedCondition();

    [GeneratedRegex(
        @"^(?:has|have|there (?:is|are))\s+(?:(?:a|an|any)\s+)?(?<relation>.+?)(?:\s+with\s+(?<condition>.+))?$",
        Options,
        250
    )]
    private static partial Regex HasRelatedCondition();

    [GeneratedRegex("[\\s-]+", Options, 250)]
    private static partial Regex EntityWhitespace();

    [GeneratedRegex(@"\bor\b", Options, 250)]
    private static partial Regex OrInValue();

    [GeneratedRegex(@"^(?:the\s+)?(?<field>.+?)\s+is\s+(?:(?<not>not)\s+)?null$", Options, 250)]
    private static partial Regex NullCondition();

    [GeneratedRegex(
        @"^(?:the\s+)?(?<field>.+?)(?:\s*(?<operation>>=|<=|<>|!=|==|[<>=])\s*|\s+(?:is\s+(?!"
            + @"not\b))?(?<operation>"
            + ScalarOperators
            + @")\s+)(?<value>.+)$",
        Options,
        250
    )]
    private static partial Regex ScalarCondition();
}
