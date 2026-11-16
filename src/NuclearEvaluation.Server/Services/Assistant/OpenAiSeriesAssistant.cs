using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class OpenAiSeriesAssistant(
    OpenAiAssistantClient client,
    LocalAssistantToolsClient tools
)
{
    public const int MaxQuestionLength = 1_000;

    internal const string Instructions = """
        Translate the user's request into exactly one typed query for the Series grid in Data Management.
        A successful query applies directly to that grid. Call build_series_grid_query if all requested
        constraints can be represented. If any criterion is unsupported or ambiguous, do not call the tool.

        The field catalog below is authoritative. Use the listed field names and enum values exactly.
        Use filters for fields on Series. Use relatedFilters to compare the count of matching rows
        of a related entity. Filters in each related count all apply to the same related row. For
        example, at least one QC sample is a Samples count with SampleType Equal Qc and count
        GreaterOrEqual 1. Empty filter arrays match all rows. All conditions are joined by AND.

        Field operands are strings. Express booleans as "true" or "false" and numbers using a decimal
        point. Use ISO dates or date-times. For IsNull and IsNotNull use a null operand.
        String operations Contains, StartsWith and EndsWith require a string field. Use equality
        comparisons on boolean and enum fields. Use relational comparisons on numbers and dates.

        Sort by a Series field or by a count of matching related rows. Set exactly one of field or
        related in order, or set order to null when sorting was not requested. Preserve conditions
        when a sort appears after them. "Top N by X" means limit N and descending order by X, and
        "Bottom N by X" means ascending order. Top or bottom without an order is ambiguous. Apply
        every explicit constraint. Never return SQL, Dynamic LINQ, or series rows.
        """;
    const string UnsupportedQuestion =
        "I couldn't build that Series query. Try specifying a Series field or a filter on related samples, subsamples, measurements, or projects.";

    internal static readonly JsonSerializerOptions QueryJson = new(JsonSerializerOptions.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter<SeriesQueryEntity>(allowIntegerValues: false),
            new JsonStringEnumConverter<SeriesComparison>(allowIntegerValues: false),
        },
    };

    internal static object BuildToolDefinition(SeriesSchemaDescription schema) =>
        new
        {
            type = "function",
            name = SeriesMcpContract.BuildQueryToolName,
            description = "Build the complete filter, sort, and limit to apply to the Series grid.",
            strict = true,
            parameters = JsonSerializer.Deserialize<JsonElement>(
                $$"""
                {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "limit": { "type": ["integer", "null"], "minimum": 1, "maximum": {{DataQuery.MaxPageSize}} },
                    "filters": { "type": "array", "items": { "$ref": "#/$defs/fieldFilter" } },
                    "relatedFilters": { "type": "array", "items": { "$ref": "#/$defs/relatedFilter" } },
                    "order": { "anyOf": [{ "$ref": "#/$defs/order" }, { "type": "null" }] }
                  },
                  "required": ["limit", "filters", "relatedFilters", "order"],
                  "$defs": {
                    "fieldFilter": {
                      "type": "object",
                      "additionalProperties": false,
                      "properties": {
                        "field": { "type": "string" },
                        "comparison": { "type": "string", "enum": {{JsonSerializer.Serialize(
                    schema.Entities.SelectMany(entity => entity.Fields).SelectMany(field =>
                        field.Comparisons
                    ).Distinct()
                )}} },
                        "value": { "type": ["string", "null"] }
                      },
                      "required": ["field", "comparison", "value"]
                    },
                    "relatedRows": {
                      "type": "object",
                      "additionalProperties": false,
                      "properties": {
                        "entity": { "type": "string", "enum": {{JsonSerializer.Serialize(
                    schema.Entities.Where(entity =>
                        entity.Entity != nameof(SeriesQueryEntity.Series)
                    ).Select(entity => entity.Entity)
                )}} },
                        "filters": { "type": "array", "items": { "$ref": "#/$defs/fieldFilter" } }
                      },
                      "required": ["entity", "filters"]
                    },
                    "relatedFilter": {
                      "type": "object",
                      "additionalProperties": false,
                      "properties": {
                        "rows": { "$ref": "#/$defs/relatedRows" },
                        "comparison": { "type": "string", "enum": {{JsonSerializer.Serialize(
                    schema.CountComparisons
                )}} },
                        "count": { "type": "integer", "minimum": 0 }
                      },
                      "required": ["rows", "comparison", "count"]
                    },
                    "order": {
                      "type": "object",
                      "additionalProperties": false,
                      "properties": {
                        "field": { "type": ["string", "null"] },
                        "related": { "anyOf": [{ "$ref": "#/$defs/relatedRows" }, { "type": "null" }] },
                        "descending": { "type": "boolean" }
                      },
                      "required": ["field", "related", "descending"]
                    }
                  }
                }
                """
            ),
        };

    public bool IsConfigured => client.IsConfigured;

    public async Task<AssistantReply> AskAsync(string message, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (message.Length > MaxQuestionLength)
            throw new ArgumentException("The assistant question is too long.", nameof(message));

        if (!client.IsConfigured)
            throw new InvalidOperationException("The OpenAI API key is not configured.");

        SeriesSchemaDescription schema = await tools.GetSeriesSchemaAsync(ct);
        string modelSchema = string.Join('\n', schema.Entities.Select(DescribeEntityForModel));
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        JsonElement[] output = await client.RequestAsync(
            $"{Instructions}\nToday's UTC date is {today}.\n\n{modelSchema}",
            message.Trim(),
            [BuildToolDefinition(schema)],
            ct
        );
        SeriesGridQuery? candidate = ReadQuery(OpenAiAssistantClient.ReadToolCall(output));
        SeriesGridQuery? query = candidate is null
            ? null
            : await tools.BuildSeriesQueryAsync(candidate, ct);
        return query is null ? new(UnsupportedQuestion, null) : new("Series query applied.", query);
    }

    internal static SeriesGridQuery? ReadQuery(OpenAiToolCall? call)
    {
        if (call?.Name != SeriesMcpContract.BuildQueryToolName)
            return null;

        try
        {
            ModelSeriesQuery? parsed = JsonSerializer.Deserialize<ModelSeriesQuery>(
                call.Arguments,
                QueryJson
            );
            return parsed is { Filters: not null, RelatedFilters: not null }
                ? new SeriesGridQuery
                {
                    Limit = parsed.Limit,
                    Filters = parsed.Filters,
                    RelatedFilters = parsed.RelatedFilters,
                    Order = parsed.Order,
                }
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string DescribeEntityForModel(SeriesEntityDescription entity) =>
        $"{entity.Entity} ({entity.RelationshipPath})\n"
        + string.Join(
            ", ",
            entity.Fields.Select(field =>
                $"{field.Name}={field.ValueType}{(field.AllowsNull ? "?" : "")}"
                + (field.EnumValues.Count > 0 ? $"[{string.Join('|', field.EnumValues)}]" : "")
            )
        );

    sealed record ModelSeriesQuery(
        int? Limit,
        IReadOnlyList<SeriesFieldFilter> Filters,
        IReadOnlyList<SeriesRelatedFilter> RelatedFilters,
        SeriesQueryOrder? Order
    );
}
