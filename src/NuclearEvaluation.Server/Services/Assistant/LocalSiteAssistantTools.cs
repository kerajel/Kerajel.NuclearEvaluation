using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Assistant;

[McpServerToolType]
internal sealed class LocalSiteAssistantTools
{
    static readonly AssistantSurface[] ScalarGrids =
    [
        AssistantSurface.Projects,
        AssistantSurface.Samples,
        AssistantSurface.SubSamples,
        AssistantSurface.Apms,
        AssistantSurface.Particles,
        AssistantSurface.StemPreview,
        AssistantSurface.StemQuality,
    ];

    static readonly SiteAssistantHelp[] Help =
    [
        new(
            "navigation",
            null,
            null,
            "Where to find the site's main features",
            "Use the sidebar to open Fleet Explorer, Generation Calendar, or Energy & Emissions. Data Management contains Series and STEM Preview. Evaluation contains Projects and Query Builder."
        ),
        new(
            "fleet_compare",
            AssistantPage.Fleet,
            null,
            "How to filter plants or compare two plants",
            "Filter the fleet by state or search for a plant. To compare two plants, select both on the map or from the plant list. The comparison shows their monthly electricity generation."
        ),
        new(
            "fleet_data",
            AssistantPage.Fleet,
            null,
            "What the fleet metrics and generation figures represent",
            "Fleet Explorer shows U.S. nuclear power plants operating at year-end 2024. Select a plant to see its reactors and capacity. The generation comparison uses monthly electricity generation from the same data snapshot."
        ),
        new(
            "fleet_states",
            AssistantPage.Fleet,
            null,
            "How to browse state rankings and filter the plant map",
            "Use State rankings to sort states by net summer capacity or annual net generation. Select a state to filter the map and plant list. The current first and second plant selections stay in place when you change states."
        ),
        new(
            "generation_calendar",
            AssistantPage.FleetGeneration,
            null,
            "How to explore monthly output and average daily generation in the calendar",
            "Choose a state and month with the dropdowns, select a calendar cell, or ask the assistant to show a month. Daily average divides monthly generation by the number of days. The detail below shows each plant's reported output for the chosen month."
        ),
        new(
            "emissions_chart",
            AssistantPage.Emissions,
            null,
            "How to read the lifecycle emissions charts or compare sources",
            "Energy & Emissions compares published lifecycle greenhouse gas estimates across electricity sources. Read the chart in grams of CO₂ equivalent per kilowatt-hour and use the lifecycle breakdown to see which phases contribute to each estimate."
        ),
        new(
            "emissions_estimate",
            AssistantPage.Emissions,
            null,
            "How to estimate total emissions for a specific generation amount",
            "Under Compare total emissions, select two sources and enter the electricity generated in MWh or GWh. The calculator applies the published lifecycle medians to that amount."
        ),
        new(
            "data_sources",
            AssistantPage.Sources,
            null,
            "Where the public site data, emissions factors, and sandbox records come from",
            "Sources & Methods links to the EIA inventory and generation data, the published lifecycle emissions estimates, and the map boundaries. The evaluation workspace uses generated example records and temporary STEM uploads."
        ),
        new(
            "series_grid",
            AssistantPage.DataManagement,
            null,
            "How to filter Series, inspect samples, or read totals",
            "Open the Series tab. Use the column filters or ask the assistant to filter, sort, or limit the grid. Expand a series to see its samples. The totals above the grid follow its filters."
        ),
        new(
            "stem_upload",
            AssistantPage.DataManagement,
            null,
            "How to upload spreadsheet data or preview staged rows",
            "Open Data Management, then STEM Preview. Add an Excel or delimited text file and upload it. The preview grid combines the staged rows. It supports filtering by lab code, analysis date, the NU flag, and isotope measurement fields."
        ),
        new(
            "stem_quality",
            AssistantPage.DataManagement,
            null,
            "How to inspect missing values, repeated identifiers, or other file quality checks",
            "Open Quality workbench and select a staged file. You can also choose Load synthetic example. Select a check to see the rows with repeated identifiers, blank lab codes, or missing U234, ErU234, U235, or ErU235 values."
        ),
        new(
            "projects_grid",
            AssistantPage.Evaluation,
            null,
            "How to filter or open Evaluation projects",
            "Use the Projects tab to find a project by name, sample count, or date. Open a project to inspect its overview and related Series, Samples, SubSamples, APM, and Particle tabs."
        ),
        new(
            "query_builder",
            AssistantPage.Evaluation,
            null,
            "How to combine cross-entity filters, change the results grid, or save a preset",
            "Open Query Builder and enable the entity filters you want to combine. Pick the result entity in the Results dropdown. An assistant request can fill the draft and refresh that grid. Use the builder's Save control when you want to keep a preset."
        ),
        new(
            "query_builder_scope",
            AssistantPage.Evaluation,
            AssistantSurface.QueryBuilder,
            "Difference between an editable Query Builder draft and filtering its current results grid",
            "Ask the assistant to build a query when you want the filter editors populated across entities. Ask it to filter the current results when you want an additional sort, limit, or filter on the selected grid. Clear the assistant query to remove that extra grid restriction."
        ),
        new(
            "project_overview",
            AssistantPage.Project,
            null,
            "How to explore the current project, its scope, or decay correction",
            "The project tabs show records belonging to this project. Open Series, Samples, SubSamples, APM, or Particles to browse them. The APM and Particle views include isotope charts and support the project's decay-correction option."
        ),
    ];

    [McpServerTool(
        Name = "describe_site_assistant",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description(
        "Describe the site's approved help and the fields, relationships, and operations for its assistant."
    )]
    public SiteAssistantCatalog Describe()
    {
        SeriesSchemaDescription series = LocalSeriesTools.DescribeGridSchema();
        return new(
            series,
            ScalarGrids
                .Select(surface => new SiteAssistantGridSchema(
                    surface,
                    AssistantGridQueries
                        .Fields(surface)
                        .Select(LocalSeriesTools.DescribeField)
                        .ToArray()
                ))
                .ToArray(),
            series
                .Entities.Where(entity =>
                    Enum.TryParse(entity.Entity, out SeriesQueryEntity value)
                    && value != SeriesQueryEntity.Projects
                )
                .Select(entity =>
                    entity with
                    {
                        Fields = entity
                            .Fields.Where(field =>
                                AssistantQueryBuilderFields.IsAllowed(
                                    Enum.Parse<SeriesQueryEntity>(entity.Entity),
                                    field.Name
                                )
                            )
                            .ToArray(),
                    }
                )
                .ToArray(),
            Help
        );
    }

    [McpServerTool(
        Name = "get_site_help",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description("Return the application's help text for an approved topic on the current page.")]
    public SiteAssistantReply GetHelp(SiteAssistantContext context, string topic)
    {
        SiteAssistantHelp? match = Help.FirstOrDefault(item =>
            item.Topic == topic && item.Matches(context)
        );
        if (!IsContextValid(context) || match is null)
            throw new McpException("The help topic is unavailable in this view.");

        return new(match.Answer);
    }

    [McpServerTool(
        Name = "validate_site_assistant_action",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false,
        UseStructuredContent = true
    )]
    [Description("Validate a typed assistant action for the currently active view.")]
    public SiteAssistantReply Validate(SiteAssistantContext context, AssistantAction action)
    {
        if (!IsContextValid(context) || !IsActionValid(context, action))
            throw new McpException("The proposed action is invalid for this view.");

        string message = action.Kind switch
        {
            AssistantActionKind.FilterGrid => "Updated the current grid.",
            AssistantActionKind.ClearGrid => "Cleared the assistant query.",
            AssistantActionKind.DraftQuery => "Updated the Query Builder draft and results.",
            AssistantActionKind.ShowQueryBuilderExample =>
                $"Loaded the {QueryBuilderExample.Advanced.Title} draft. It returns particles from completed Regular series, "
                    + "QC samples, and non-legacy subsamples with an APM U-235 value. Each particle has a U-234 value and U-234 error at or below 0.2.",
            AssistantActionKind.ShowStemPreview => "Opened the STEM preview.",
            AssistantActionKind.ShowStemQuality => "Opened the selected file's quality profile.",
            AssistantActionKind.CheckStemQuality =>
                "Showing the rows that match the quality check.",
            AssistantActionKind.SelectGenerationView => "Updated the Generation Calendar.",
            _ => throw new McpException("The action is unavailable."),
        };
        return new(message, action);
    }

    internal static bool IsContextValid(SiteAssistantContext? context)
    {
        if (context is null)
            return false;

        bool surfaceMatches = context.Page switch
        {
            AssistantPage.DataManagement => context.Surface
                is AssistantSurface.None
                    or AssistantSurface.Series
                    or AssistantSurface.StemPreview
                    or AssistantSurface.StemQuality,
            AssistantPage.Evaluation => context.Surface
                is AssistantSurface.None
                    or AssistantSurface.Projects
                    or AssistantSurface.QueryBuilder,
            AssistantPage.Project => context.Surface
                is AssistantSurface.None
                    or AssistantSurface.Series
                    or AssistantSurface.Samples
                    or AssistantSurface.SubSamples
                    or AssistantSurface.Apms
                    or AssistantSurface.Particles,
            AssistantPage.FleetGeneration => context.Surface
                is AssistantSurface.None
                    or AssistantSurface.GenerationCalendar,
            AssistantPage.Home
            or AssistantPage.Fleet
            or AssistantPage.Emissions
            or AssistantPage.Sources
            or AssistantPage.Unknown => context.Surface == AssistantSurface.None,
            _ => false,
        };
        return surfaceMatches
            && (
                context.Surface == AssistantSurface.QueryBuilder
                    ? context.ResultGrid
                        is AssistantSurface.Series
                            or AssistantSurface.Samples
                            or AssistantSurface.SubSamples
                            or AssistantSurface.Apms
                            or AssistantSurface.Particles
                    : context.ResultGrid == AssistantSurface.None
            )
            && (
                !context.HasUploads
                || context.Surface is AssistantSurface.StemPreview or AssistantSurface.StemQuality
            );
    }

    internal static bool CanFilter(SiteAssistantContext context) =>
        context.ActiveGrid == AssistantSurface.Series
        || (
            ScalarGrids.Contains(context.ActiveGrid)
            && (
                context.ActiveGrid
                    is not (AssistantSurface.StemPreview or AssistantSurface.StemQuality)
                || context.HasUploads
            )
        );

    static bool IsActionValid(SiteAssistantContext context, AssistantAction? action)
    {
        if (
            action is null
            || (
                action.Kind != AssistantActionKind.SelectGenerationView
                && action.GenerationSelection is not null
            )
        )
            return false;

        bool isStem =
            context.Surface is AssistantSurface.StemPreview or AssistantSurface.StemQuality;
        return action.Kind switch
        {
            AssistantActionKind.FilterGrid when context.ActiveGrid == AssistantSurface.Series =>
                action.SeriesQuery is { } series
                    && action.GridQuery is null
                    && action.BuilderFilters is null
                    && action.QualityCheck is null
                    && LocalSeriesTools.IsValid(series),
            AssistantActionKind.FilterGrid => CanFilter(context)
                && action.GridQuery is { } grid
                && grid.Grid == context.ActiveGrid
                && action.SeriesQuery is null
                && action.BuilderFilters is null
                && action.QualityCheck is null
                && AssistantGridQueries.IsValid(grid),
            AssistantActionKind.DraftQuery => context.Surface == AssistantSurface.QueryBuilder
                && action.SeriesQuery is null
                && action.GridQuery is null
                && action.QualityCheck is null
                && action.BuilderFilters
                    is { Count: <= AssistantQueryBuilderFields.MaxFilters } filters
                && filters.All(IsBuilderFilterValid),
            AssistantActionKind.CheckStemQuality => isStem
                && context.HasUploads
                && action.QualityCheck is { } check
                && Enum.IsDefined(check)
                && action.SeriesQuery is null
                && action.GridQuery is null
                && action.BuilderFilters is null,
            AssistantActionKind.SelectGenerationView => context.Surface
                == AssistantSurface.GenerationCalendar
                && action.SeriesQuery is null
                && action.GridQuery is null
                && action.BuilderFilters is null
                && action.QualityCheck is null
                && action.GenerationSelection is { } selection
                && IsGenerationSelectionValid(selection),
            _ => action.SeriesQuery is null
                && action.GridQuery is null
                && action.BuilderFilters is null
                && action.QualityCheck is null
                && (
                    action.Kind switch
                    {
                        AssistantActionKind.ClearGrid => CanFilter(context),
                        AssistantActionKind.ShowStemPreview => isStem,
                        AssistantActionKind.ShowStemQuality => isStem && context.HasUploads,
                        AssistantActionKind.ShowQueryBuilderExample =>
                            QueryBuilderExample.Advanced.Filters.All(IsBuilderFilterValid),
                        _ => false,
                    }
                ),
        };
    }

    static bool IsBuilderFilterValid(AssistantBuilderFilter? entry) =>
        entry?.Filter is { } filter
        && AssistantQueryBuilderFields.IsAllowed(entry.Entity, filter.Field)
        && SeriesQuerySchema.FindField(entry.Entity, filter.Field) is { } field
        && SeriesQuerySchema.TryReadValue(field, filter, out _);

    static bool IsGenerationSelectionValid(AssistantGenerationSelection selection) =>
        (
            selection.StateCode is not null
            || selection.Month.HasValue
            || selection.DailyAverage.HasValue
        )
        && (
            selection.StateCode is null
            || selection.StateCode.Length == 2 && selection.StateCode.All(char.IsAsciiLetterUpper)
        )
        && selection.Month is null or (>= 1 and <= 12);
}

internal sealed record SiteAssistantCatalog(
    SeriesSchemaDescription Series,
    IReadOnlyList<SiteAssistantGridSchema> Grids,
    IReadOnlyList<SeriesEntityDescription> Builder,
    IReadOnlyList<SiteAssistantHelp> Help
);

internal sealed record SiteAssistantGridSchema(
    AssistantSurface Grid,
    IReadOnlyList<SeriesFieldDescription> Fields
);

internal sealed record SiteAssistantHelp(
    string Topic,
    AssistantPage? Page,
    AssistantSurface? Surface,
    string Description,
    string Answer
)
{
    public bool Matches(SiteAssistantContext context) =>
        (Page is null || Page == context.Page) && (Surface is null || Surface == context.Surface);
}
