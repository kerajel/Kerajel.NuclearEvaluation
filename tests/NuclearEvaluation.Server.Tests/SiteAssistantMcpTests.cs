using System.Text.Json;
using ModelContextProtocol;
using NuclearEvaluation.Server.Services.Assistant;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Tests;

public class SiteAssistantMcpTests
{
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
