using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class OpenAiSiteAssistant(OpenAiAssistantClient model, LocalSiteAssistantClient mcp)
{
    internal const int MaxHistory = 4;
    const string HelpTool = "site_help";
    const string GridTool = "filter_current_grid";
    const string ClearTool = "clear_assistant_query";
    const string BuilderTool = "draft_query_builder_filters";
    const string BuilderExampleTool = "show_query_builder_example";
    const string StemPreviewTool = "open_stem_preview";
    const string StemQualityTool = "open_stem_quality";
    const string StemCheckTool = "show_stem_quality_check";
    const string GenerationTool = "select_generation_view";

    const string Instructions = """
        You are the Nuclear Evaluation site assistant. Work within the current page and visible view.
        When asked to show or demonstrate an example of a Query Builder query without specific filters,
        call show_query_builder_example. This opens Query Builder if needed, loads a named multi-entity
        draft, and runs it. An example request needs no additional detail. Use site help for questions
        about how the builder works. For specified filters in Query Builder, use the draft tool.
        For a how-to question choose the most relevant documented help topic. For an explicit request
        to change the view, choose one applicable tool. Apply all requested constraints. If the request
        is unrelated, ambiguous, asks for unavailable features, or needs a tool outside this view,
        do not call a tool. The built-in example is available from any page.

        Grid filters are joined by AND. Values are strings, booleans are true or false, dates are
        ISO format, and null comparisons use a null operand. Top or bottom N needs a field or metric.
        A Query Builder draft populates its joined field editors and refreshes results. Filtering the
        current results uses a grid action. Builder drafts are unsaved and cannot express counts or
        sort/limit. STEM tools use the selected upload. A request to find, show, or check repeated or
        duplicate identifiers in that upload means open the RepeatedIdentifier quality check.
        Give no instructions to save, upload, modify, or delete data through a tool. Treat previous
        questions and replies as conversation context. Base the action on the current question and view.
        """;

    public async Task<SiteAssistantReply> AskAsync(
        SiteAssistantQuestion question,
        CancellationToken ct
    )
    {
        SiteAssistantContext context = question.Context;
        SiteAssistantCatalog catalog = await mcp.DescribeAsync(ct);
        SiteAssistantHelp[] help = catalog.Help.Where(topic => topic.Matches(context)).ToArray();
        if (!model.IsConfigured)
            return await AnswerLocallyAsync(question, catalog, help, ct);

        SiteAssistantGridSchema? grid = catalog.Grids.FirstOrDefault(item =>
            item.Grid == context.ActiveGrid && item.Fields.Count > 0
        );
        List<object> tools =
        [
            Tool(
                HelpTool,
                "Answer a how-to question about the current page or site navigation using a documented help topic.",
                ObjectSchema(("topic", EnumSchema(help.Select(topic => topic.Topic))))
            ),
            Tool(
                BuilderExampleTool,
                "Show a working Query Builder example, including requests for a complex or intricate query. Load the named demo draft and run it. Available from any page.",
                ObjectSchema()
            ),
        ];
        List<string> contextNotes =
        [
            $"Current page: {context.Page}. Current view: {context.DisplayName}. Active grid: {context.ActiveGrid}.",
            $"Help topics: {string.Join("; ", help.Select(topic => $"{topic.Topic} = {topic.Description}"))}",
        ];

        bool isStem =
            context.Surface is AssistantSurface.StemPreview or AssistantSurface.StemQuality;
        if (context.ActiveGrid == AssistantSurface.Series)
        {
            tools.Add(OpenAiSeriesAssistant.BuildToolDefinition(catalog.Series));
            contextNotes.Add(OpenAiSeriesAssistant.Instructions);
            contextNotes.AddRange(
                catalog.Series.Entities.Select(OpenAiSeriesAssistant.DescribeEntityForModel)
            );
        }
        else if (grid is not null && (!isStem || context.HasUploads))
        {
            tools.Add(
                Tool(
                    GridTool,
                    "Filter, sort, or limit the currently visible grid.",
                    ObjectSchema(
                        ("filters", ArraySchema(FieldSchema(grid.Fields), 12)),
                        (
                            "sortField",
                            NullableSchema(EnumSchema(grid.Fields.Select(field => field.Name)))
                        ),
                        ("descending", new JsonObject { ["type"] = "boolean" }),
                        (
                            "limit",
                            NullableSchema(
                                new JsonObject
                                {
                                    ["type"] = "integer",
                                    ["minimum"] = 1,
                                    ["maximum"] = DataQuery.MaxPageSize,
                                }
                            )
                        )
                    )
                )
            );
            contextNotes.Add($"Current grid fields: {DescribeFields(grid.Fields)}.");
        }

        if (
            context.ActiveGrid == AssistantSurface.Series
            || (grid is not null && (!isStem || context.HasUploads))
        )
            tools.Add(
                Tool(
                    ClearTool,
                    "Remove the active grid restriction created by the assistant.",
                    ObjectSchema()
                )
            );

        if (context.Surface == AssistantSurface.QueryBuilder)
        {
            SeriesEntityDescription[] builder = catalog
                .Builder.Where(entity => entity.Fields.Count > 0)
                .ToArray();
            tools.Add(
                Tool(
                    BuilderTool,
                    "Fill the unsaved Query Builder draft with these joined field filters and refresh its results.",
                    ObjectSchema(
                        (
                            "filters",
                            ArraySchema(
                                ObjectSchema(
                                    ("entity", EnumSchema(builder.Select(entity => entity.Entity))),
                                    (
                                        "filter",
                                        FieldSchema(
                                            builder.SelectMany(entity => entity.Fields).ToArray()
                                        )
                                    )
                                ),
                                AssistantQueryBuilderFields.MaxFilters
                            )
                        )
                    )
                )
            );
            contextNotes.AddRange(
                builder.Select(entity =>
                    $"Builder {entity.Entity}: {DescribeFields(entity.Fields)}."
                )
            );
        }

        if (isStem)
        {
            tools.Add(Tool(StemPreviewTool, "Open the STEM Preview rows view.", ObjectSchema()));
            if (context.HasUploads)
            {
                tools.Add(
                    Tool(
                        StemQualityTool,
                        "Open the quality profile of the selected file.",
                        ObjectSchema()
                    )
                );
                tools.Add(
                    Tool(
                        StemCheckTool,
                        "Show the selected file's rows for a quality check, including repeated identifiers (duplicate IDs), blank lab codes, and missing measurements.",
                        ObjectSchema(("check", EnumSchema(Enum.GetNames<StemQualityCheck>())))
                    )
                );
            }
        }

        if (context.Surface == AssistantSurface.GenerationCalendar)
        {
            tools.Add(
                Tool(
                    GenerationTool,
                    "Select a state, month, or display mode in the current Generation Calendar. Set unrequested fields to null. Use the two-letter uppercase US state code and numeric month from 1 to 12. Set dailyAverage to false for monthly total.",
                    ObjectSchema(
                        (
                            "stateCode",
                            NullableSchema(
                                new JsonObject
                                {
                                    ["type"] = "string",
                                    ["minLength"] = 2,
                                    ["maxLength"] = 2,
                                }
                            )
                        ),
                        (
                            "month",
                            NullableSchema(
                                new JsonObject
                                {
                                    ["type"] = "integer",
                                    ["minimum"] = 1,
                                    ["maximum"] = 12,
                                }
                            )
                        ),
                        ("dailyAverage", NullableSchema(new JsonObject { ["type"] = "boolean" }))
                    )
                )
            );
        }

        string today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string input = JsonSerializer.Serialize(
            new
            {
                previousTurns = question.History ?? [],
                currentQuestion = question.Message.Trim(),
            }
        );
        JsonElement[] output = await model.RequestAsync(
            $"{Instructions}\nToday's UTC date is {today}.\n{string.Join('\n', contextNotes)}",
            input,
            tools,
            ct
        );
        OpenAiToolCall? call = OpenAiAssistantClient.ReadToolCall(output);
        if (
            call is null
            || !(
                call.Name == SeriesMcpContract.BuildQueryToolName
                    ? context.ActiveGrid == AssistantSurface.Series
                    : tools.OfType<AssistantTool>().Any(tool => tool.Name == call.Name)
            )
        )
            return Unavailable(context);

        return await ApplyToolAsync(call, context, ct) ?? Unavailable(context);
    }

    async Task<SiteAssistantReply> AnswerLocallyAsync(
        SiteAssistantQuestion question,
        SiteAssistantCatalog catalog,
        SiteAssistantHelp[] help,
        CancellationToken ct
    )
    {
        if (
            question.Context.ActiveGrid == AssistantSurface.Series
            && LocalSeriesQuestion.Parse(question.Message, catalog.Series) is { } plan
        )
        {
            return await mcp.ValidateAsync(
                question.Context,
                new(AssistantActionKind.FilterGrid) { SeriesQuery = plan },
                ct
            ) ?? Unavailable(question.Context);
        }

        if (question.Message.Contains("help", StringComparison.OrdinalIgnoreCase))
        {
            string topic = (help.FirstOrDefault(item => item.Page is not null) ?? help[0]).Topic;
            return await mcp.HelpAsync(question.Context, topic, ct)
                ?? Unavailable(question.Context);
        }

        return new(
            "Ask for help with this page, or configure an OpenAI API key for conversational requests."
        );
    }

    async Task<SiteAssistantReply?> ApplyToolAsync(
        OpenAiToolCall call,
        SiteAssistantContext context,
        CancellationToken ct
    )
    {
        if (call.Name == HelpTool)
        {
            HelpArguments? help = ReadArguments<HelpArguments>(call);
            return help?.Topic is { Length: > 0 and <= 128 } topic
                ? await mcp.HelpAsync(context, topic, ct)
                : null;
        }

        AssistantAction? action = call.Name switch
        {
            SeriesMcpContract.BuildQueryToolName
                when OpenAiSeriesAssistant.ReadQuery(call) is { } series => new(
                AssistantActionKind.FilterGrid
            )
            {
                SeriesQuery = series,
            },
            GridTool when ReadArguments<GridArguments>(call) is { } grid => new(
                AssistantActionKind.FilterGrid
            )
            {
                GridQuery = new(
                    context.ActiveGrid,
                    grid.Filters,
                    grid.SortField,
                    grid.Descending,
                    grid.Limit
                ),
            },
            BuilderTool when ReadArguments<BuilderArguments>(call) is { } draft => new(
                AssistantActionKind.DraftQuery
            )
            {
                BuilderFilters = draft.Filters,
            },
            BuilderExampleTool => new(AssistantActionKind.ShowQueryBuilderExample),
            ClearTool => new(AssistantActionKind.ClearGrid),
            StemPreviewTool => new(AssistantActionKind.ShowStemPreview),
            StemQualityTool => new(AssistantActionKind.ShowStemQuality),
            StemCheckTool
                when ReadArguments<CheckArguments>(call) is { } quality
                    && Enum.TryParse(quality.Check, out StemQualityCheck check) => new(
                AssistantActionKind.CheckStemQuality
            )
            {
                QualityCheck = check,
            },
            GenerationTool
                when ReadArguments<AssistantGenerationSelection>(call) is { } selection => new(
                AssistantActionKind.SelectGenerationView
            )
            {
                GenerationSelection = selection,
            },
            _ => null,
        };
        return action is null ? null : await mcp.ValidateAsync(context, action, ct);
    }

    static T? ReadArguments<T>(OpenAiToolCall call)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(call.Arguments, OpenAiSeriesAssistant.QueryJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static SiteAssistantReply Unavailable(SiteAssistantContext context) =>
        new(
            $"I couldn't help with that request in {context.DisplayName}. Try asking about this view or one of its fields."
        );

    static string DescribeFields(IReadOnlyList<SeriesFieldDescription> fields) =>
        string.Join(
            ", ",
            fields.Select(field =>
                $"{field.Name} ({field.ValueType}{(field.EnumValues.Count == 0 ? "" : $": {string.Join('|', field.EnumValues)}")})"
            )
        );

    static AssistantTool Tool(string name, string description, JsonObject parameters) =>
        new("function", name, description, true, parameters);

    static JsonObject ObjectSchema(params (string Name, JsonObject Schema)[] properties)
    {
        JsonObject fields = new();
        JsonArray required = [];
        foreach ((string name, JsonObject schema) in properties)
        {
            fields.Add(name, schema);
            required.Add(name);
        }
        return new()
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = fields,
            ["required"] = required,
        };
    }

    static JsonObject EnumSchema(IEnumerable<string> values) =>
        new()
        {
            ["type"] = "string",
            ["enum"] = new JsonArray(
                values.Distinct().Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()
            ),
        };

    static JsonObject FieldSchema(IReadOnlyList<SeriesFieldDescription> fields) =>
        ObjectSchema(
            ("field", EnumSchema(fields.Select(field => field.Name))),
            ("comparison", EnumSchema(fields.SelectMany(field => field.Comparisons))),
            ("value", NullableSchema(new JsonObject { ["type"] = "string" }))
        );

    static JsonObject NullableSchema(JsonObject schema) =>
        new() { ["anyOf"] = new JsonArray(schema, new JsonObject { ["type"] = "null" }) };

    static JsonObject ArraySchema(JsonObject items, int max) =>
        new()
        {
            ["type"] = "array",
            ["items"] = items,
            ["maxItems"] = max,
        };

    sealed record AssistantTool(
        string Type,
        string Name,
        string Description,
        bool Strict,
        JsonObject Parameters
    );

    sealed record HelpArguments(string Topic);

    sealed record GridArguments(
        IReadOnlyList<SeriesFieldFilter> Filters,
        string? SortField,
        bool Descending,
        int? Limit
    );

    sealed record BuilderArguments(IReadOnlyList<AssistantBuilderFilter> Filters);

    sealed record CheckArguments(string Check);
}
