using System.Text.Json;
using ModelContextProtocol;
using NuclearEvaluation.Server.Services.Assistant;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Tests;

public class SiteAssistantMcpTests
{
    [Fact]
    public void WorkbenchProjectObservationActionsUseThePublishedGridSchema()
    {
        LocalSiteAssistantTools tools = new();
        SiteAssistantContext context = new(
            AssistantPage.Workbench,
            AssistantSurface.WorkbenchProjectObservations
        );
        WorkbenchAssistantGridSchema grid = Assert.Single(
            tools.Describe().WorkbenchGrids!,
            schema => schema.Surface == context.Surface
        );
        Assert.Contains(grid.Fields, field =>
            field.Name == nameof(RadNetRow.DoseNsvH)
            && field.Type == RadNetFieldType.Number
            && field.Nullable
        );
        Assert.Contains(grid.Fields, field => field.Name == nameof(RadNetRow.State));

        ColumnFilterGroup dose = new(
            nameof(RadNetRow.DoseNsvH),
            false,
            [new(RadNetOperator.AtLeast, "30"), new(RadNetOperator.LessThan, "500")]
        );
        AssistantWorkbenchGridQuery query = new(
            [
                dose,
                new(
                    nameof(RadNetRow.State),
                    true,
                    [new(RadNetOperator.Equal, "CA"), new(RadNetOperator.Equal, "OR")]
                ),
            ],
            nameof(RadNetRow.DoseNsvH),
            Descending: true,
            PageSize: 10
        );
        AssistantAction action = WorkbenchAction(query);
        AssistantAction clear = new(AssistantActionKind.ClearGrid);

        Assert.Equal(action, tools.Validate(context, action).Action);
        Assert.Equal(clear, tools.Validate(context, clear).Action);
        Assert.Throws<McpException>(() =>
            tools.Validate(new(AssistantPage.Workbench, AssistantSurface.None), action)
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(new(AssistantPage.Radiation, AssistantSurface.None), action)
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(context, action with { RadNetQuery = new() })
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(context, WorkbenchAction(query with { Filters = [dose, dose] }))
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(
                context,
                WorkbenchAction(query with { PageSize = RadNetCatalog.MaxPageSize + 1 })
            )
        );
    }

    [Fact]
    public void WorkbenchQueryResultActionsAreRestrictedToTheSelectedResultEntity()
    {
        LocalSiteAssistantTools tools = new();
        SiteAssistantContext channelResults = new(
            AssistantPage.Workbench,
            AssistantSurface.WorkbenchQueryResults,
            WorkbenchResultEntity: WorkbenchQueryEntity.Channel
        );
        WorkbenchAssistantGridSchema grid = Assert.Single(
            tools.Describe().WorkbenchGrids!,
            schema =>
                schema.Surface == channelResults.Surface
                && schema.ResultEntity == channelResults.WorkbenchResultEntity
        );
        Assert.Contains(grid.Fields, field => field.Name == nameof(WorkbenchQueryRow.CountRate));

        ColumnFilterGroup countRate = new(
            nameof(WorkbenchQueryRow.CountRate),
            false,
            [new(RadNetOperator.AtLeast, "100")]
        );
        AssistantWorkbenchGridQuery query = new([countRate], nameof(WorkbenchQueryRow.ObservedAt));
        AssistantAction action = WorkbenchAction(query);

        Assert.Equal(action, tools.Validate(channelResults, action).Action);
        Assert.Throws<McpException>(() =>
            tools.Validate(
                channelResults with
                {
                    WorkbenchResultEntity = WorkbenchQueryEntity.Observation,
                },
                action
            )
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(channelResults with { WorkbenchResultEntity = null }, action)
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(
                channelResults,
                WorkbenchAction(
                    query with
                    {
                        Filters =
                        [
                            countRate with
                            {
                                Conditions = [new(RadNetOperator.AtLeast, "NaN")],
                            },
                        ],
                    }
                )
            )
        );
    }

    [Theory]
    [InlineData(
        AssistantSurface.WorkbenchSeries,
        nameof(WorkbenchSeries.From),
        "2025-01-01",
        "2025-02-30"
    )]
    [InlineData(
        AssistantSurface.WorkbenchSeries,
        nameof(WorkbenchSeries.Review),
        nameof(WorkbenchReview.Pending),
        "Ignored"
    )]
    [InlineData(
        AssistantSurface.WorkbenchImportRows,
        nameof(ImportReviewRow.Disposition),
        nameof(ImportReviewDisposition.Duplicate),
        "Duplicate row"
    )]
    public void WorkbenchActionsValidateGridSpecificValues(
        AssistantSurface surface,
        string field,
        string valid,
        string invalid
    )
    {
        LocalSiteAssistantTools tools = new();
        SiteAssistantContext context = new(AssistantPage.Workbench, surface);
        AssistantAction Action(string value) =>
            WorkbenchAction(new([new(field, false, [new(RadNetOperator.Equal, value)])]));

        AssistantAction validAction = Action(valid);
        Assert.Equal(validAction, tools.Validate(context, validAction).Action);
        Assert.Throws<McpException>(() => tools.Validate(context, Action(invalid)));
    }

    static AssistantAction WorkbenchAction(AssistantWorkbenchGridQuery query) =>
        new(AssistantActionKind.ApplyWorkbenchGridQuery) { WorkbenchGridQuery = query };

    [Fact]
    public void ValidatesRadiochemistryQueriesAndExampleAgainstTheActivePage()
    {
        LocalSiteAssistantTools tools = new();
        SiteAssistantContext radiochemistry = new(
            AssistantPage.Radiochemistry,
            AssistantSurface.None
        );
        SiteAssistantContext home = new(AssistantPage.Home, AssistantSurface.None);
        AssistantAction action = new(AssistantActionKind.ApplyRemDbQuery)
        {
            RemDbQuery = RemDbExample.Query,
        };
        AssistantAction example = new(AssistantActionKind.ShowRemDbExample);

        Assert.Contains(tools.Describe().RemDbFields!, field =>
            field.Key == nameof(RemDbRow.Qualifier)
        );
        Assert.Equal(action, tools.Validate(radiochemistry, action).Action);
        Assert.Equal(example, tools.Validate(home, example).Action);
        Assert.Throws<McpException>(() => tools.Validate(home, action));
        Assert.Throws<McpException>(() =>
            tools.Validate(
                radiochemistry,
                action with
                {
                    RemDbQuery = RemDbExample.Query with { SortField = "Unknown" },
                }
            )
        );
    }

    [Fact]
    public void ValidatesRadNetQueriesAgainstTheRadiationCatalog()
    {
        LocalSiteAssistantTools tools = new();
        SiteAssistantContext radiation = new(AssistantPage.Radiation, AssistantSurface.None);
        AssistantAction example = new(AssistantActionKind.ApplyRadNetQuery)
        {
            RadNetQuery = RadNetExample.Query,
        };

        Assert.Contains(tools.Describe().RadNetFields!, field =>
            field.Name == nameof(RadNetRow.Channel)
        );
        Assert.Equal(example, tools.Validate(radiation, example).Action);
        Assert.Throws<McpException>(() =>
            tools.Validate(new(AssistantPage.Evaluation, AssistantSurface.Projects), example)
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(
                radiation,
                example with
                {
                    RadNetQuery = RadNetExample.Query with { SortField = "UnknownField" },
                }
            )
        );
    }

    [Fact]
    public void ProjectsCatalogAndValidationSupportRankedSeriesFilters()
    {
        LocalSiteAssistantTools tools = new();
        SiteAssistantGridSchema projects = Assert.Single(tools.Describe().Grids, grid =>
            grid.Grid == AssistantSurface.Projects
        );
        Assert.Contains(projects.RelatedSeries!.Fields, field =>
            field.Name == "SampleCount"
            && field.Comparisons.Contains(nameof(SeriesComparison.GreaterOrEqual))
        );

        SiteAssistantContext context = new(AssistantPage.Evaluation, AssistantSurface.Projects);
        AssistantGridQuery query = new(AssistantSurface.Projects, [], "UpdatedAt", true, 10)
        {
            ProjectSeriesRanking = new(
                [new("SampleCount", SeriesComparison.GreaterOrEqual, "10")],
                true
            ),
        };
        AssistantAction action = new(AssistantActionKind.FilterGrid) { GridQuery = query };
        Assert.Equal(action, tools.Validate(context, action).Action);
        Assert.Throws<McpException>(() =>
            tools.Validate(context, action with { GridQuery = query with { Limit = null } })
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(
                context,
                action with
                {
                    GridQuery = query with
                    {
                        ProjectSeriesRanking = new(
                            [new("UpdatedAt", SeriesComparison.Equal, "2025-01-01")],
                            true
                        ),
                    },
                }
            )
        );
    }

    static readonly SiteAssistantContext Calendar = new(
        AssistantPage.FleetGeneration,
        AssistantSurface.GenerationCalendar
    );

    [Fact]
    public void ReadsTheNamedQualityCheckReturnedByMcp()
    {
        const string response = """
            {
                "message": "Showing the rows that match the quality check.",
                "action": { "kind": "CheckStemQuality", "qualityCheck": "RepeatedIdentifier" }
            }
            """;

        SiteAssistantReply? reply = JsonSerializer.Deserialize<SiteAssistantReply>(
            response,
            LocalAssistantToolsClient.McpJson
        );

        AssistantAction action = Assert.IsType<AssistantAction>(reply?.Action);
        Assert.Equal(AssistantActionKind.CheckStemQuality, action.Kind);
        Assert.Equal(StemQualityCheck.RepeatedIdentifier, action.QualityCheck);
    }

    [Fact]
    public void CalendarActionAcceptsAPartialSelectionOnlyInTheCalendar()
    {
        LocalSiteAssistantTools tools = new();
        AssistantAction action = new(AssistantActionKind.SelectGenerationView)
        {
            GenerationSelection = new(null, 2, true),
        };

        Assert.Equal(action, tools.Validate(Calendar, action).Action);
        Assert.Throws<McpException>(() =>
            tools.Validate(new(AssistantPage.FleetGeneration, AssistantSurface.None), action)
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(new(AssistantPage.Fleet, AssistantSurface.None), action)
        );
    }

    [Theory]
    [InlineData("Illinois", 2)]
    [InlineData("IL", 13)]
    [InlineData("IL", 0)]
    [InlineData(null, null)]
    public void CalendarActionRejectsMalformedOrEmptySelections(string? state, int? month)
    {
        AssistantAction action = new(AssistantActionKind.SelectGenerationView)
        {
            GenerationSelection = new(state, month, null),
        };

        Assert.Throws<McpException>(() => new LocalSiteAssistantTools().Validate(Calendar, action));
    }

    [Theory]
    [InlineData(AssistantPage.Home, AssistantSurface.None, AssistantSurface.None)]
    [InlineData(AssistantPage.Evaluation, AssistantSurface.Projects, AssistantSurface.None)]
    [InlineData(AssistantPage.Evaluation, AssistantSurface.QueryBuilder, AssistantSurface.Series)]
    public void ValidatesTheNamedQueryBuilderExampleFromAnyView(
        AssistantPage page,
        AssistantSurface surface,
        AssistantSurface result
    )
    {
        LocalSiteAssistantTools tools = new();
        SiteAssistantContext context = new(page, surface, result);
        AssistantAction example = new(AssistantActionKind.ShowQueryBuilderExample);

        SiteAssistantReply reply = tools.Validate(context, example);

        Assert.Equal(example, reply.Action);
        Assert.Contains(
            QueryBuilderExample.Advanced.Title,
            reply.Message,
            StringComparison.Ordinal
        );
        Assert.Throws<McpException>(() =>
            tools.Validate(context, example with { BuilderFilters = [] })
        );
    }
}
