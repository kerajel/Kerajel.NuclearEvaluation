using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Server.Services.RemDb;
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
            "Open Radiation Monitoring for EPA RadNet observations or Radiochemistry for the European Commission's REMdb radionuclide measurements. Both have a Query Builder and a source-file quality view. The sidebar also links to the U.S. nuclear fleet, generation, and emissions."
        ),
        new(
            "workbench_overview",
            AssistantPage.Workbench,
            null,
            "How to open Series, Project observations, Query Builder results, or Import Review",
            "Open Series to manage observation ranges, Projects to see linked observations, or Query Builder to combine Project, Series, file, observation, and channel conditions. Import Review checks staged CSV rows against the RadNet archive. You can filter and sort the active grid from its column headers or with the assistant."
        ),
        new(
            "workbench_project_observations",
            AssistantPage.Workbench,
            AssistantSurface.WorkbenchProjectObservations,
            "How filtering applies to the selected Project observations grid",
            "Project observations come from the Series linked to the selected Project. Filtering or sorting the grid leaves its membership and coverage chart in place. Use CSV row links to inspect the corresponding source files."
        ),
        new(
            "workbench_query_results",
            AssistantPage.Workbench,
            AssistantSurface.WorkbenchQueryResults,
            "How to refine the current Query Builder results or change the draft",
            "Use Query Builder's filters and Results dropdown to define and run a draft. Then use the result columns or ask the assistant to refine the current results. An additional column filter applies to the selected result level, and clearing those column filters keeps the draft in place."
        ),
        new(
            "radnet_browse",
            AssistantPage.Radiation,
            null,
            "How to browse stations, source files, observations, or gamma readings, and use Radiation Query Builder",
            "Open a monitoring station, then an annual source file, an observation in UTC, and its gamma channel readings. In Query Builder you can combine conditions across those levels, choose the results to return, and save a named query in this browser."
        ),
        new(
            "radnet_quality",
            AssistantPage.Radiation,
            null,
            "How to inspect archive quality, gaps, source rows, and published units",
            "File Quality shows the selected archive member's date range, input and retained records, repeated timestamps, missing hours, and channel coverage. A missing value means the source supplied no usable reading for that field. The archive keeps gamma count rates in counts per minute and dose-equivalent rates in nSv/h."
        ),
        new(
            "remdb_browse",
            AssistantPage.Radiochemistry,
            null,
            "How to browse REMdb sites, sampling windows and measurements or build a query",
            "Open a site, then a sampling window, then a measurement. The window groups rows by site, medium, treatment, and published timestamps. Query Builder can filter across these levels and save named drafts in your browser."
        ),
        new(
            "remdb_quality",
            AssistantPage.Radiochemistry,
            null,
            "How to interpret published units, reporting limits, missing qualifiers, and source quality metrics",
            "File Quality reports result counts, missing activity values, less-than qualifiers, and coverage by unit and value type. A less-than sign marks a reported bound. The 1984–2011 exports have no dedicated qualifier column. Compare activity only for compatible units, media, and radionuclides."
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
            "Where monitoring, radiochemistry, fleet, generation, and emissions data come from",
            "Sources & Methods links to EPA RadNet, the European Commission JRC REMdb archive and reuse terms, the EIA inventory and generation data, published lifecycle emissions estimates, and map boundaries."
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
            "Use the Projects tab to find a project by name, sample count, or date. The assistant can rank projects by the count of linked series that match your filters. Open a project to inspect its overview and related records."
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
                        .ToArray(),
                    surface == AssistantSurface.Projects
                        ? series.Entities.Single(entity =>
                            entity.Entity == nameof(SeriesQueryEntity.Series)
                        ) with
                        {
                            RelationshipPath = "Projects > Project series links > Series",
                        }
                        : null
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
            Help,
            RadNetCatalog.Fields,
            RemDbCatalog.Fields,
            WorkbenchAssistantCatalog.Grids
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
            AssistantActionKind.FilterGrid
                when action.GridQuery?.ProjectSeriesRanking is { Filters.Count: > 0 } =>
                "Ranked the Projects grid by the number of linked series matching your filters.",
            AssistantActionKind.FilterGrid
                when action.GridQuery?.ProjectSeriesRanking is not null =>
                "Ranked the Projects grid by the number of linked series.",
            AssistantActionKind.FilterGrid => "Updated the current grid.",
            AssistantActionKind.ApplyWorkbenchGridQuery => "Updated the current grid.",
            AssistantActionKind.ClearGrid when context.Page == AssistantPage.Workbench =>
                "Cleared the current grid’s column filters.",
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
            AssistantActionKind.ApplyRadNetQuery =>
                "Applied the filters in Radiation Query Builder.",
            AssistantActionKind.ApplyRemDbQuery =>
                "Applied the filters in Radiochemistry Query Builder.",
            AssistantActionKind.ShowRemDbExample =>
                $"Opened the {RemDbExample.Title} example in Radiochemistry Query Builder.",
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
            AssistantPage.Workbench => context.Surface == AssistantSurface.None
                || WorkbenchAssistantQueries.FindGrid(context, WorkbenchAssistantCatalog.Grids)
                    is not null,
            AssistantPage.Home
            or AssistantPage.Fleet
            or AssistantPage.Emissions
            or AssistantPage.Sources
            or AssistantPage.Radiation
            or AssistantPage.Radiochemistry
            or AssistantPage.Unknown => context.Surface == AssistantSurface.None,
            _ => false,
        };
        return surfaceMatches
            && (
                context.WorkbenchResultEntity is null
                || context.Surface == AssistantSurface.WorkbenchQueryResults
            )
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
            || (
                action.Kind != AssistantActionKind.ApplyRadNetQuery
                && action.RadNetQuery is not null
            )
            || (action.Kind != AssistantActionKind.ApplyRemDbQuery && action.RemDbQuery is not null)
            || (
                action.Kind != AssistantActionKind.ApplyWorkbenchGridQuery
                && action.WorkbenchGridQuery is not null
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
            AssistantActionKind.ApplyRadNetQuery => context.Page == AssistantPage.Radiation
                && action.SeriesQuery is null
                && action.GridQuery is null
                && action.BuilderFilters is null
                && action.QualityCheck is null
                && IsRadNetQueryValid(action.RadNetQuery),
            AssistantActionKind.ApplyRemDbQuery => context.Page == AssistantPage.Radiochemistry
                && action.SeriesQuery is null
                && action.GridQuery is null
                && action.BuilderFilters is null
                && action.QualityCheck is null
                && IsRemDbQueryValid(action.RemDbQuery),
            AssistantActionKind.ApplyWorkbenchGridQuery => action.SeriesQuery is null
                && action.GridQuery is null
                && action.BuilderFilters is null
                && action.QualityCheck is null
                && WorkbenchAssistantQueries.IsValid(context, action.WorkbenchGridQuery),
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
                        AssistantActionKind.ClearGrid => CanFilter(context)
                            || WorkbenchAssistantQueries.FindGrid(
                                context,
                                WorkbenchAssistantCatalog.Grids
                            )
                                is not null,
                        AssistantActionKind.ShowStemPreview => isStem,
                        AssistantActionKind.ShowStemQuality => isStem && context.HasUploads,
                        AssistantActionKind.ShowQueryBuilderExample =>
                            QueryBuilderExample.Advanced.Filters.All(IsBuilderFilterValid),
                        AssistantActionKind.ShowRemDbExample => context.Page
                            is AssistantPage.Home
                                or AssistantPage.Sources,
                        _ => false,
                    }
                ),
        };
    }

    static bool IsRadNetQueryValid(RadNetQuery? query)
    {
        if (query is null || query.Skip != 0)
            return false;

        try
        {
            RadNetQueryPlan.Compile(query);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    static bool IsBuilderFilterValid(AssistantBuilderFilter? entry) =>
        entry?.Filter is { } filter
        && AssistantQueryBuilderFields.IsAllowed(entry.Entity, filter.Field)
        && SeriesQuerySchema.FindField(entry.Entity, filter.Field) is { } field
        && SeriesQuerySchema.TryReadValue(field, filter, out _);

    static bool IsRemDbQueryValid(RemDbQuery? query)
    {
        if (query is null || query.Skip != 0)
            return false;

        try
        {
            RemDbQueryPlan.Compile(query);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

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
    IReadOnlyList<SiteAssistantHelp> Help,
    IReadOnlyList<RadNetField>? RadNetFields = null,
    IReadOnlyList<RemDbField>? RemDbFields = null,
    IReadOnlyList<WorkbenchAssistantGridSchema>? WorkbenchGrids = null
);

internal sealed record SiteAssistantGridSchema(
    AssistantSurface Grid,
    IReadOnlyList<SeriesFieldDescription> Fields,
    SeriesEntityDescription? RelatedSeries = null
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
