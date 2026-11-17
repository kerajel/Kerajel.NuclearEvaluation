using System.Data.Common;
using System.Globalization;
using DuckDB.NET.Data;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Workbench;

static class ImportReviewQueryPlan
{
    internal static async Task<ImportReviewPage> ReadAsync(
        DuckDBConnection database,
        ImportReviewQuery query,
        CancellationToken ct
    )
    {
        if (query.Skip < 0 || query.Top < 1 || query.Top > ImportReviewCatalog.MaxPageSize)
            throw new ArgumentException("Choose a valid import review page.");

        string sortField = string.IsNullOrWhiteSpace(query.SortField)
            ? "source_row"
            : Column(FindField(query.SortField));
        string order = $"{sortField} {(query.Descending ? "DESC" : "ASC")} NULLS LAST";
        if (sortField != "source_row")
            order += ", source_row";

        Dictionary<string, object> arguments = [];
        string where = Filter(query.Filters ?? [], arguments);
        await using DuckDBCommand countCommand = RadNetSnapshot.Command(
            database,
            "SELECT count(*) FROM review_rows" + where,
            arguments
        );
        long count = (long)(await countCommand.ExecuteScalarAsync(ct) ?? 0L);
        if (query.Skip >= count)
            return new([], count);

        arguments.Add("top", query.Top);
        arguments.Add("skip", query.Skip);
        await using DuckDBCommand pageCommand = RadNetSnapshot.Command(
            database,
            $"""
            SELECT source_row, location, observed_at, status, exposure_mr_h, dose_nsv_h, r03,
                disposition, reason
            FROM review_rows{where}
            ORDER BY {order} LIMIT $top OFFSET $skip
            """,
            arguments
        );
        await using DbDataReader reader = await pageCommand.ExecuteReaderAsync(ct);
        List<ImportReviewRow> items = new(query.Top);
        while (await reader.ReadAsync(ct))
        {
            items.Add(
                new(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.IsDBNull(2)
                        ? null
                        : DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc),
                    reader.GetString(3),
                    ReadNumber(reader, 4),
                    ReadNumber(reader, 5),
                    ReadNumber(reader, 6),
                    Enum.Parse<ImportReviewDisposition>(reader.GetString(7)),
                    reader.GetString(8)
                )
            );
        }

        return new(items, count);
    }

    static double? ReadNumber(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);

    static string Filter(
        IReadOnlyList<ColumnFilterGroup> groups,
        Dictionary<string, object> arguments
    )
    {
        if (groups.Count > ImportReviewCatalog.Fields.Count)
            throw new ArgumentException("Choose at most one filter group per column.");

        List<string> predicates = [];
        HashSet<string> columns = new(StringComparer.OrdinalIgnoreCase);
        foreach (ColumnFilterGroup group in groups)
        {
            if (
                group is null
                || group.Conditions is not { Count: > 0 and <= ColumnFilterGroup.MaxConditions }
            )
                throw new ArgumentException("Choose one or two conditions per column.");

            ImportReviewField field = FindField(group.Field);
            if (!columns.Add(field.Name))
                throw new ArgumentException("Choose at most one filter group per column.");

            string conditions = string.Join(
                group.MatchAny ? " OR " : " AND ",
                group.Conditions.Select(condition => Condition(field, condition, arguments))
            );
            predicates.Add($"({conditions})");
        }

        return predicates.Count == 0 ? "" : " WHERE " + string.Join(" AND ", predicates);
    }

    static string Condition(
        ImportReviewField field,
        ColumnFilterCondition condition,
        Dictionary<string, object> arguments
    )
    {
        if (condition is null || !ImportReviewCatalog.CanCompare(field, condition.Operator))
            throw new ArgumentException($"The comparison is unavailable for {field.Label}.");

        string column = Column(field);
        if (condition.Operator == RadNetOperator.IsPresent)
            return column + " IS NOT NULL";
        if (condition.Operator == RadNetOperator.IsMissing)
            return column + " IS NULL";

        string name = "filter" + arguments.Count.ToString(CultureInfo.InvariantCulture);
        arguments.Add(name, ReadValue(field, condition.Value));
        string parameter = "$" + name;
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

    static object ReadValue(ImportReviewField field, string? value)
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
                when value.Length >= 10
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

    static ImportReviewField FindField(string field) =>
        ImportReviewCatalog.Fields.FirstOrDefault(value =>
            string.Equals(value.Name, field, StringComparison.OrdinalIgnoreCase)
        ) ?? throw new ArgumentException("Choose a column in the import review grid.");

    static string Column(ImportReviewField field) =>
        field.Name switch
        {
            nameof(ImportReviewRow.SourceRow) => "source_row",
            nameof(ImportReviewRow.Location) => "location",
            nameof(ImportReviewRow.ObservedAt) => "observed_at",
            nameof(ImportReviewRow.Status) => "status",
            nameof(ImportReviewRow.ExposureMrH) => "exposure_mr_h",
            nameof(ImportReviewRow.DoseNsvH) => "dose_nsv_h",
            nameof(ImportReviewRow.R03) => "r03",
            nameof(ImportReviewRow.Disposition) => "disposition",
            nameof(ImportReviewRow.Reason) => "reason",
            _ => throw new ArgumentException("Choose a column in the import review grid."),
        };
}
