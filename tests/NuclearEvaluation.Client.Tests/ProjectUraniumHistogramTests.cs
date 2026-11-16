using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuclearEvaluation.Client.Pages;
using NuclearEvaluation.Client.Shared.Charts;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Plotting;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;

namespace NuclearEvaluation.Client.Tests;

public class ProjectUraniumHistogramTests : TestBase
{
    public ProjectUraniumHistogramTests()
    {
        TestContext
            .JSInterop.Setup<Rect>("Radzen.createChart", _ => true)
            .SetResult(new Rect { Width = 640, Height = 360 });
    }

    [Theory]
    [InlineData("Apm", "apm", "apm-isotope-distribution")]
    [InlineData("Particles", "particles", "particle-isotope-distribution")]
    public void ProjectDistributionLinkKeepsTheActiveTabAndFocusesItsChart(
        string tabTitle,
        string tab,
        string targetId
    )
    {
        NavigationManager navigation = TestContext.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/projects/23");
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                DataResult<ProjectView>.Succeeded([new() { Id = 23, Name = "Project data" }], 1)
            );
        IRenderedComponent<ProjectCard> project = TestContext.Render<ProjectCard>(parameters =>
            parameters.Add(x => x.Id, 23)
        );

        project
            .FindAll("[role=tab]")
            .Single(element => element.TextContent.Trim() == tabTitle)
            .Click();
        AngleSharp.Dom.IElement link = project.WaitForElement(".project-distribution-link");

        Assert.Equal(
            new Uri(new(navigation.BaseUri), $"projects/23?tab={tab}#{targetId}").AbsoluteUri,
            link.GetAttribute("href")
        );
        Assert.Equal("-1", project.Find($"#{targetId}").GetAttribute("tabindex"));
        const string focus = "Blazor._internal.domWrapper.focus";
        int previousFocusCalls = TestContext.JSInterop.Invocations[focus].Count;
        link.Click();

        TestContext.JSInterop.VerifyInvoke(focus, previousFocusCalls + 1);
        Assert.Equal(tabTitle, project.Find("[role=tab][aria-selected=true]").TextContent.Trim());
    }

    [Fact]
    public async Task ApmChartUsesTheGridQueryAndShowsAllFourIsotopeLinesWithCounts()
    {
        DataQuery query = new()
        {
            ProjectId = 23,
            Filter = "U234 >= 1",
            DecayCorrected = true,
        };
        Api.GetProjectApmUraniumBinCounts(query, Arg.Any<CancellationToken>())
            .Returns([
                Isotope("U234", 3, 1),
                Isotope("U235", 2, 2),
                Isotope("U236", 0, 4),
                Isotope("U238", 4, 0),
            ]);
        IRenderedComponent<ProjectApmUraniumBinCountsChart> chart =
            TestContext.Render<ProjectApmUraniumBinCountsChart>(parameters =>
                parameters.Add(x => x.ProjectId, 23).Add(x => x.Query, query)
            );

        IRenderedComponent<RadzenLineSeries<BinCount>>[] series = chart
            .FindComponents<RadzenLineSeries<BinCount>>()
            .ToArray();
        Assert.Equal(["U234", "U235", "U236", "U238"], series.Select(line => line.Instance.Title));
        Assert.All(series, line =>
        {
            Assert.Equal(LineType.Dashed, line.Instance.LineType);
            Assert.Equal(nameof(BinCount.Name), line.Instance.CategoryProperty);
            Assert.Equal(nameof(BinCount.Count), line.Instance.ValueProperty);
        });
        IEnumerable<BinCount>? bins = series[0].Instance.Data;
        Assert.NotNull(bins);
        Assert.Equal(["n.m.", "1-2"], bins.Select(bin => bin.Name));
        Assert.Equal([1, 3], bins.Select(bin => bin.Count));
        IRenderedComponent<RadzenMarkers>[] markers = chart
            .FindComponents<RadzenMarkers>()
            .ToArray();
        Assert.Equal(4, markers.Length);
        Assert.All(markers, marker =>
        {
            Assert.True(marker.Instance.Visible);
            Assert.Equal(MarkerType.Square, marker.Instance.MarkerType);
        });
        IRenderedComponent<RadzenSeriesDataLabels>[] labels = chart
            .FindComponents<RadzenSeriesDataLabels>()
            .ToArray();
        Assert.Equal(4, labels.Length);
        Assert.All(labels, label => Assert.True(label.Instance.Visible));
        Assert.True(chart.FindComponent<RadzenChartTooltipOptions>().Instance.Shared);
        await Api.Received(1).GetProjectApmUraniumBinCounts(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ParticleChartUsesProjectIdFallbackAndCanRetryIntoAnEmptyResult()
    {
        Api.GetProjectParticleUraniumBinCounts(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<List<IsotopeBinCounts>>(new HttpRequestException()));
        IRenderedComponent<ProjectParticleUraniumBinCountsChart> chart =
            TestContext.Render<ProjectParticleUraniumBinCountsChart>(parameters =>
                parameters.Add(x => x.ProjectId, 19)
            );

        Assert.Equal("The chart could not be loaded.", chart.Find("[role='alert']").TextContent);

        Api.GetProjectParticleUraniumBinCounts(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns([Isotope("U234", 2, 1), Isotope("U235", 1, 2)]);
        chart
            .FindAll("button")
            .Single(button => button.TextContent.Trim() == "Retry chart")
            .Click();

        Assert.Equal(
            ["U234", "U235"],
            chart.FindComponents<RadzenLineSeries<BinCount>>().Select(line => line.Instance.Title)
        );
        Api.GetProjectParticleUraniumBinCounts(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);
        await chart.InvokeAsync(chart.Instance.Refresh);

        Assert.Equal("No matching rows.", chart.Find("[role='status']").TextContent);
        Assert.Empty(chart.FindComponents<RadzenLineSeries<BinCount>>());
        await Api.Received(3)
            .GetProjectParticleUraniumBinCounts(
                Arg.Is<DataQuery>(query => query.ProjectId == 19 && !query.DecayCorrected),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ProjectHidesThePreviousChartWhileTheMeasurementGridRefreshes()
    {
        TestContext
            .Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/projects/23?tab=apm");
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                DataResult<ProjectView>.Succeeded([new() { Id = 23, Name = "Project data" }], 1)
            );
        const string filter = "U234 >= 1";
        TaskCompletionSource<DataResult<ApmView>> pendingGrid = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Api.GetApmViews(
                Arg.Is<DataQuery>(query => query.Filter == filter),
                Arg.Any<CancellationToken>()
            )
            .Returns(pendingGrid.Task);
        Api.GetProjectApmUraniumBinCounts(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                call.Arg<DataQuery>().Filter == filter
                    ? [Isotope("U234", 2, 0), Isotope("U235", 1, 1)]
                    : [Isotope("U234", 4, 0), Isotope("U235", 3, 1)]
            );

        IRenderedComponent<ProjectCard> project = TestContext.Render<ProjectCard>(parameters =>
            parameters.Add(x => x.Id, 23)
        );
        project.WaitForAssertion(() => Assert.Equal(4, U234Rows(project)));

        IRenderedComponent<ApmGrid> grid = project.FindComponent<ApmGrid>();
        Task reload = grid.InvokeAsync(() =>
            grid.Instance.LoadData(new LoadDataArgs { Filter = filter })
        );

        project.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Waiting for the measurement grid…",
                project.Find("#apm-isotope-distribution [role='status']").TextContent
            );
            Assert.Empty(project.FindComponents<RadzenLineSeries<BinCount>>());
        });
        pendingGrid.SetResult(DataResult<ApmView>.Succeeded([], 0));
        await reload;

        project.WaitForAssertion(() => Assert.Equal(2, U234Rows(project)));
        await Api.Received()
            .GetProjectApmUraniumBinCounts(
                Arg.Is<DataQuery>(query => query.ProjectId == 23 && query.Filter == filter),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ChangingQueryCancelsThePreviousRequestAndKeepsTheNewResult()
    {
        DataQuery firstQuery = new() { ProjectId = 23 };
        DataQuery changedQuery = new() { ProjectId = 23, Filter = "U234 >= 1" };
        TaskCompletionSource<List<IsotopeBinCounts>> pending = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        CancellationToken firstCancellation = default;
        Api.GetProjectApmUraniumBinCounts(firstQuery, Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                firstCancellation = call.Arg<CancellationToken>();
                return pending.Task;
            });
        Api.GetProjectApmUraniumBinCounts(changedQuery, Arg.Any<CancellationToken>())
            .Returns([Isotope("U234", 2, 1)]);
        IRenderedComponent<ProjectApmUraniumBinCountsChart> chart =
            TestContext.Render<ProjectApmUraniumBinCountsChart>(parameters =>
                parameters.Add(x => x.Query, firstQuery)
            );

        Assert.Equal("Loading uranium distributions…", chart.Find("[role='status']").TextContent);
        chart.Render(parameters => parameters.Add(x => x.Query, changedQuery));
        Assert.True(firstCancellation.IsCancellationRequested);
        Assert.Equal(3, U234Rows(chart));

        pending.SetResult([Isotope("U234", 18, 4)]);
        await chart.InvokeAsync(async () => await Task.Yield());
        Assert.Equal(3, U234Rows(chart));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OldMeasurementGridCannotReplaceTheChartAfterTheProjectOrTabChanges(
        bool changeProject
    )
    {
        TestContext
            .Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/projects/23?tab=apm");
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                int projectId = call.Arg<DataQuery>().Filter == "Id == 47" ? 47 : 23;
                return DataResult<ProjectView>.Succeeded(
                    [new() { Id = projectId, Name = $"Project {projectId}" }],
                    1
                );
            });
        const string staleFilter = "U234 >= 9";
        TaskCompletionSource<DataResult<ApmView>> pendingGrid = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Api.GetApmViews(
                Arg.Is<DataQuery>(query => query.Filter == staleFilter),
                Arg.Any<CancellationToken>()
            )
            .Returns(pendingGrid.Task);
        Api.GetProjectApmUraniumBinCounts(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => [Isotope("U234", call.Arg<DataQuery>().ProjectId == 47 ? 8 : 4, 0)]);

        IRenderedComponent<ProjectCard> project = TestContext.Render<ProjectCard>(parameters =>
            parameters.Add(x => x.Id, 23)
        );
        project.WaitForAssertion(() => Assert.Equal(4, U234Rows(project)));
        IRenderedComponent<ApmGrid> oldGrid = project.FindComponent<ApmGrid>();
        ApmGrid oldInstance = oldGrid.Instance;
        Task staleLoad = oldGrid.InvokeAsync(() =>
            oldInstance.LoadData(new LoadDataArgs { Filter = staleFilter })
        );
        project.WaitForAssertion(() =>
            Assert.Equal(
                "Waiting for the measurement grid…",
                project.Find("#apm-isotope-distribution [role='status']").TextContent
            )
        );

        if (changeProject)
        {
            project.Render(parameters => parameters.Add(x => x.Id, 47));
        }
        else
        {
            project
                .FindAll("[role=tab]")
                .Single(tab => tab.TextContent.Trim() == "Samples")
                .Click();
            project.FindAll("[role=tab]").Single(tab => tab.TextContent.Trim() == "Apm").Click();
        }

        int expectedRows = changeProject ? 8 : 4;
        project.WaitForAssertion(() => Assert.Equal(expectedRows, U234Rows(project)));
        Assert.NotSame(oldInstance, project.FindComponent<ApmGrid>().Instance);

        pendingGrid.SetResult(DataResult<ApmView>.Succeeded([], 0));
        await staleLoad;

        Assert.Equal(expectedRows, U234Rows(project));
        await Api.DidNotReceive()
            .GetProjectApmUraniumBinCounts(
                Arg.Is<DataQuery>(query => query.Filter == staleFilter),
                Arg.Any<CancellationToken>()
            );
    }

    static int U234Rows(IRenderedComponent<IComponent> chart)
    {
        IEnumerable<BinCount>? bins = chart
            .FindComponents<RadzenLineSeries<BinCount>>()
            .Single(line => line.Instance.Title == "U234")
            .Instance.Data;
        Assert.NotNull(bins);
        return bins.Sum(bin => bin.Count);
    }

    static IsotopeBinCounts Isotope(string isotope, int measured, int missing) =>
        new()
        {
            Isotope = isotope,
            Bins =
            [
                new() { Name = "n.m.", Count = missing },
                new() { Name = "1-2", Count = measured },
            ],
        };
}
