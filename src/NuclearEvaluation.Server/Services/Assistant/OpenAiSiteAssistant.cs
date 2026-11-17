using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class OpenAiSiteAssistant(OpenAiAssistantClient model, LocalSiteAssistantClient mcp)
{
    internal const int MaxHistory = 4;
    const string HelpTool = "site_help";
    const string GridTool = "filter_current_grid";
    const string WorkbenchGridTool = "apply_workbench_grid_query";
    const string RankProjectsTool = "rank_projects_by_series_count";
    const string ClearTool = "clear_assistant_query";
    const string BuilderTool = "draft_query_builder_filters";
    const string BuilderExampleTool = "show_query_builder_example";
    const string RadNetTool = "apply_radnet_query";
    const string RadNetExampleTool = "show_radnet_query_example";
    const string RemDbTool = "apply_remdb_query";
    const string RemDbExampleTool = "show_remdb_query_example";
    const string StemPreviewTool = "open_stem_preview";
    const string StemQualityTool = "open_stem_quality";
    const string StemCheckTool = "show_stem_quality_check";
    const string GenerationTool = "select_generation_view";

    const string Instructions = """
        You are the Nuclear Evaluation site assistant. Work within the current page and visible view.
        When asked to demonstrate a Query Builder query without specific filters, call the available
        example tool for the current page.
        This opens the relevant Query Builder, loads a multi-entity draft, and runs it. An example
        request needs no additional detail. Use site help for questions
        about how the builder works. When a draft tool is available and the user asks to build a draft,
        use it. Use the current grid tool when the user asks to refine displayed results.
        For a how-to question choose the most relevant documented help topic. For an explicit request
        to change the view, choose one applicable tool. Apply all requested constraints. If the request
        is unrelated, ambiguous, asks for unavailable features, or needs a tool outside this view,
        do not call a tool.

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
        WorkbenchAssistantGridSchema? workbenchGrid = WorkbenchAssistantQueries.FindGrid(
            context,
            catalog.WorkbenchGrids
        );
        List<object> tools =
        [
            Tool(
                HelpTool,
                "Answer a how-to question about the current page or site navigation using a documented help topic.",
                ObjectSchema(("topic", EnumSchema(help.Select(topic => topic.Topic))))
            ),
        ];
        List<string> contextNotes =
        [
            $"Current page: {context.Page}. Current view: {context.DisplayName}. Active grid: {context.ActiveGrid}.",
            $"Help topics: {string.Join("; ", help.Select(topic => $"{topic.Topic} = {topic.Description}"))}",
        ];

        if (
            context.Page == AssistantPage.Radiation
            && catalog.RadNetFields is { Count: > 0 } radNet
        )
        {
            tools.Add(
                BuildDatasetQueryTool(
                    RadNetTool,
                    "Fill and run Radiation Query Builder. Choose a sort field on the result entity or one of its parents.",
                    radNet.Select(field => (field.Entity.ToString(), field.Name)).ToArray(),
                    RadNetCatalog.MaxGroups,
                    RadNetCatalog.MaxConditions,
                    RadNetCatalog.MaxPageSize
                )
            );
            tools.Add(
                Tool(
                    RadNetExampleTool,
                    "Open and run an example query across RadNet stations, observations, and gamma readings.",
                    ObjectSchema()
                )
            );
            contextNotes.Add(
                "RadNet hierarchy: Station > annual SourceFile > timestamped Observation > gamma Channel reading. State uses the two-letter postal code. Group conditions on one record using matchAny false for AND. For a channel threshold, put Channel and CountRate conditions in the same Channel group. CountRate is CPM. ObservedAt is UTC. Use ISO dates and strings for values. Use 50 rows per page by default. RadNet Query Builder supports filtering and sorting."
            );
            contextNotes.AddRange(
                radNet
                    .GroupBy(field => field.Entity)
                    .Select(group =>
                        $"RadNet {group.Key}: {string.Join(", ", group.Select(field => $"{field.Name} ({field.Label}, {field.Type})"))}."
                    )
            );
        }
        else if (
            context.Page
            is AssistantPage.Radiochemistry
                or AssistantPage.Home
                or AssistantPage.Sources
        )
        {
            tools.Add(
                Tool(
                    RemDbExampleTool,
                    "Open and run a named example across REMdb sites, sampling windows, and radionuclide measurements.",
                    ObjectSchema()
                )
            );
            if (
                context.Page == AssistantPage.Radiochemistry
                && catalog.RemDbFields is { Count: > 0 } remDb
            )
            {
                tools.Add(
                    BuildDatasetQueryTool(
                        RemDbTool,
                        "Fill and run Radiochemistry Query Builder. Choose a sort field on the result entity or one of its parents.",
                        remDb.Select(field => (field.Entity.ToString(), field.Key)).ToArray(),
                        RemDbCatalog.MaxGroups,
                        RemDbCatalog.MaxConditions,
                        RemDbCatalog.MaxPageSize
                    )
                );
                contextNotes.Add(
                    """
                    REMdb hierarchy: Site > SamplingWindow > Measurement. Use published date strings YYYY-MM-DD. SourceYear is the annual file label. Country uses names (Austria), Medium is a published phrase (use Contains for MILK COW), Nuclide uses codes such as CS-137, Unit uses labels like BQ/M3, and Qualifier '<' is a reported upper bound. Group conditions on one result using matchAny false for AND. Scope activity comparisons to compatible units, media and radionuclides. Do not treat bounds as detected activity. Use 50 rows per page by default.
                    """
                );
                contextNotes.AddRange(
                    remDb
                        .GroupBy(field => field.Entity)
                        .Select(group =>
                            $"REMdb {group.Key}: {string.Join(", ", group.Select(field => $"{field.Key} ({field.Label}, {field.Type})"))}."
                        )
                );
            }
        }
        else if (
            context.Page
            is AssistantPage.Evaluation
                or AssistantPage.DataManagement
                or AssistantPage.Project
        )
        {
            tools.Add(
                Tool(
                    BuilderExampleTool,
                    "Show a working isotope-lab Query Builder example, including requests for a complex or intricate query. Load the named demo draft and run it.",
                    ObjectSchema()
                )
            );
        }

        bool isStem =
            context.Surface is AssistantSurface.StemPreview or AssistantSurface.StemQuality;
        if (workbenchGrid is not null)
        {
            tools.Add(BuildWorkbenchGridTool(workbenchGrid));
            tools.Add(
                Tool(
                    ClearTool,
                    "Clear the current Data Workbench grid's column filters. Keep its sort, page size, selected Project, and Query Builder draft.",
                    ObjectSchema()
                )
            );
            contextNotes.Add(DescribeWorkbenchGrid(workbenchGrid));
        }
        else if (context.ActiveGrid == AssistantSurface.Series)
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

        if (grid is { Grid: AssistantSurface.Projects, RelatedSeries: { } relatedSeries })
        {
            tools.Add(BuildProjectRankingTool(grid, relatedSeries));
            contextNotes.Add(
                """
                In Projects, "where sample count" refers to the total in the project's Sample Count
                column. Use projectFilters for that total and individualSeriesFilters for conditions
                explicitly scoped to individual series.
                Select the requested number of projects by series count first, then use the requested
                column sort on that selection.
                """
            );
            contextNotes.Add(
                $"Fields for each linked series: {DescribeFields(relatedSeries.Fields)}."
            );
        }

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

        return await ApplyToolAsync(call, question, ct) ?? Unavailable(context);
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
        SiteAssistantQuestion question,
        CancellationToken ct
    )
    {
        SiteAssistantContext context = question.Context;
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
            WorkbenchGridTool when ReadArguments<AssistantWorkbenchGridQuery>(call) is { } grid =>
                new(AssistantActionKind.ApplyWorkbenchGridQuery) { WorkbenchGridQuery = grid },
            RankProjectsTool
                when ReadArguments<ProjectRankingArguments>(call)
                    is { ProjectFilters: not null, IndividualSeriesFilters: not null } projects =>
                new(AssistantActionKind.FilterGrid)
                {
                    GridQuery = BuildProjectRankingQuery(projects, question.Message),
                },
            BuilderTool when ReadArguments<BuilderArguments>(call) is { } draft => new(
                AssistantActionKind.DraftQuery
            )
            {
                BuilderFilters = draft.Filters,
            },
            BuilderExampleTool => new(AssistantActionKind.ShowQueryBuilderExample),
            RadNetTool when ReadArguments<RadNetQuery>(call) is { } query => new(
                AssistantActionKind.ApplyRadNetQuery
            )
            {
                RadNetQuery = query,
            },
            RadNetExampleTool => new(AssistantActionKind.ApplyRadNetQuery)
            {
                RadNetQuery = RadNetExample.Query,
            },
            RemDbTool when ReadArguments<RemDbQuery>(call) is { } query => new(
                AssistantActionKind.ApplyRemDbQuery
            )
            {
                RemDbQuery = query,
            },
            RemDbExampleTool when context.Page == AssistantPage.Radiochemistry => new(
                AssistantActionKind.ApplyRemDbQuery
            )
            {
                RemDbQuery = RemDbExample.Query,
            },
            RemDbExampleTool => new(AssistantActionKind.ShowRemDbExample),
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

    static AssistantTool BuildWorkbenchGridTool(WorkbenchAssistantGridSchema grid)
    {
        JsonObject conditions = ArraySchema(
            ObjectSchema(
                ("operator", EnumSchema(Enum.GetNames<RadNetOperator>())),
                ("value", NullableSchema(new JsonObject { ["type"] = "string" }))
            ),
            ColumnFilterGroup.MaxConditions
        );
        conditions["minItems"] = 1;
        return Tool(
            WorkbenchGridTool,
            "Apply per-column filters, a sort, or a page size directly to the current Data Workbench grid while keeping its data scope.",
            ObjectSchema(
                (
                    "filters",
                    ArraySchema(
                        ObjectSchema(
                            ("field", EnumSchema(grid.Fields.Select(field => field.Name))),
                            ("matchAny", new JsonObject { ["type"] = "boolean" }),
                            ("conditions", conditions)
                        ),
                        WorkbenchAssistantQueries.MaxGroups(grid)
                    )
                ),
                ("sortField", NullableSchema(EnumSchema(grid.Fields.Select(field => field.Name)))),
                ("descending", new JsonObject { ["type"] = "boolean" }),
                (
                    "pageSize",
                    NullableSchema(
                        new JsonObject
                        {
                            ["type"] = "integer",
                            ["minimum"] = 1,
                            ["maximum"] = RadNetCatalog.MaxPageSize,
                        }
                    )
                )
            )
        );
    }

    static string DescribeWorkbenchGrid(WorkbenchAssistantGridSchema grid)
    {
        string fields = string.Join(
            ", ",
            grid.Fields.Select(field =>
                $"{field.Name} ({field.Label}, {field.Type}{(field.Nullable ? ", optional" : "")}{(field.Values is { Count: > 0 } values ? $", values: {string.Join('|', values)}" : "")})"
            )
        );
        string scope = grid.Surface switch
        {
            AssistantSurface.WorkbenchSeries =>
                "Each row describes a Series of observations. For From and To, use YYYY-MM-DD.",
            AssistantSurface.WorkbenchProjectObservations =>
                "Each row is a RadNet observation for the selected Project. Preserve the selected Project and its coverage chart.",
            AssistantSurface.WorkbenchQueryResults =>
                $"Each row is a {grid.ResultEntity} result of the current Query Builder draft. Only refine its results with columns listed here.",
            AssistantSurface.WorkbenchImportRows =>
                "Each row is a staged CSV record with its archive comparison outcome.",
            _ => "Each row is a RadNet observation from the retained archive.",
        };
        return $"""
            Current Data Workbench grid fields: {fields}.
            {scope}
            Filter only on these columns. Use one group per affected column, with one or two conditions.
            Combine groups with AND. Within one column, matchAny true means OR and false means AND.
            Use canonical field and enum value spellings. Use Contains only for free-text fields;
            compare numbers and dates by range where applicable. Use ISO dates or UTC timestamps.
            For a year, use an inclusive start and exclusive end on the same date field.
            Use the two-letter state code for State. IsMissing and IsPresent require an optional
            column and a null value. To preserve settings, leave sortField or pageSize null.
            For top or bottom N, supply the sort field, direction, and pageSize N to show the first N rows.
            Page size sets rows per page and the grid retains pagination. Use clear_assistant_query
            only for an explicit request to clear the grid's column filters.
            """;
    }

    static AssistantTool BuildDatasetQueryTool(
        string toolName,
        string description,
        IReadOnlyList<(string Entity, string Name)> fields,
        int maxGroups,
        int maxConditions,
        int maxPageSize
    ) =>
        Tool(
            toolName,
            description,
            ObjectSchema(
                ("entity", EnumSchema(fields.Select(field => field.Entity))),
                (
                    "filters",
                    ArraySchema(
                        ObjectSchema(
                            ("entity", EnumSchema(fields.Select(field => field.Entity))),
                            ("matchAny", new JsonObject { ["type"] = "boolean" }),
                            (
                                "conditions",
                                ArraySchema(
                                    ObjectSchema(
                                        ("field", EnumSchema(fields.Select(field => field.Name))),
                                        ("operator", EnumSchema(Enum.GetNames<RadNetOperator>())),
                                        (
                                            "value",
                                            NullableSchema(new JsonObject { ["type"] = "string" })
                                        )
                                    ),
                                    maxConditions
                                )
                            )
                        ),
                        maxGroups
                    )
                ),
                ("sortField", NullableSchema(EnumSchema(fields.Select(field => field.Name)))),
                ("descending", new JsonObject { ["type"] = "boolean" }),
                (
                    "top",
                    new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["maximum"] = maxPageSize,
                    }
                )
            )
        );

    static readonly string[] IndividualSeriesQualifiers =
    [
        "each series",
        "series each",
        "per series",
        "individual series",
        "series individually",
    ];

    static AssistantGridQuery BuildProjectRankingQuery(
        ProjectRankingArguments ranking,
        string message
    )
    {
        IReadOnlyList<SeriesFieldFilter> projectFilters = ranking.ProjectFilters;
        IReadOnlyList<SeriesFieldFilter> seriesFilters = ranking.IndividualSeriesFilters;
        // Both levels have a SampleCount field. Bind the unqualified grid label to the current grid.
        if (
            message.Contains("where sample count", StringComparison.OrdinalIgnoreCase)
            && !IndividualSeriesQualifiers.Any(qualifier =>
                message.Contains(qualifier, StringComparison.OrdinalIgnoreCase)
            )
            && !projectFilters.Any(IsSampleCount)
            && seriesFilters.Any(IsSampleCount)
        )
        {
            projectFilters = [.. projectFilters, .. seriesFilters.Where(IsSampleCount)];
            seriesFilters = seriesFilters.Where(filter => !IsSampleCount(filter)).ToArray();
        }

        return new(
            AssistantSurface.Projects,
            projectFilters,
            ranking.SortField,
            ranking.Descending,
            ranking.Limit
        )
        {
            ProjectSeriesRanking = new(seriesFilters, ranking.RankDescending),
        };

        static bool IsSampleCount(SeriesFieldFilter filter) =>
            string.Equals(
                filter?.Field,
                nameof(ProjectView.SampleCount),
                StringComparison.OrdinalIgnoreCase
            );
    }

    static AssistantTool BuildProjectRankingTool(
        SiteAssistantGridSchema grid,
        SeriesEntityDescription relatedSeries
    ) =>
        Tool(
            RankProjectsTool,
            "Filter by Projects grid fields and rank by the count of linked series. Optional individual series filters restrict which links count. Apply any column sort after selecting the requested number.",
            ObjectSchema(
                ("projectFilters", ArraySchema(FieldSchema(grid.Fields), 12)),
                ("individualSeriesFilters", ArraySchema(FieldSchema(relatedSeries.Fields), 12)),
                (
                    "limit",
                    new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["maximum"] = DataQuery.MaxPageSize,
                    }
                ),
                ("rankDescending", new JsonObject { ["type"] = "boolean" }),
                ("sortField", NullableSchema(EnumSchema(grid.Fields.Select(field => field.Name)))),
                ("descending", new JsonObject { ["type"] = "boolean" })
            )
        );

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

    sealed record ProjectRankingArguments(
        IReadOnlyList<SeriesFieldFilter> ProjectFilters,
        IReadOnlyList<SeriesFieldFilter> IndividualSeriesFilters,
        int Limit,
        bool RankDescending,
        string? SortField,
        bool Descending
    );

    sealed record BuilderArguments(IReadOnlyList<AssistantBuilderFilter> Filters);

    sealed record CheckArguments(string Check);
}
