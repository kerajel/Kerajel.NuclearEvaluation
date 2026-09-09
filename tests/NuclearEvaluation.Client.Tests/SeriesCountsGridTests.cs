using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Tests;

public class SeriesCountsGridTests : TestBase
{
    [Fact]
    public void InitialRenderIsBusyAndDoesNotShowAnEmptyResult()
    {
        IRenderedComponent<SeriesCountsGrid> component = TestContext.Render<SeriesCountsGrid>();

        RadzenDataGrid<SeriesCountsView> grid = component
            .FindComponent<RadzenDataGrid<SeriesCountsView>>()
            .Instance;

        Assert.True(grid.IsLoading);
        Assert.Equal("true", component.Find(".ne-series-counts-grid").GetAttribute("aria-busy"));
        Assert.DoesNotContain("No records to display", component.Markup);
    }

    [Fact]
    public async Task CachedTotalsStayVisibleAndBusyUntilFreshTotalsArrive()
    {
        SeriesCountsView cached = new()
        {
            SeriesCount = 10,
            SampleCount = 20,
            SubSampleCount = 30,
            ApmCount = 40,
            ParticleCount = 50,
        };
        SeriesCountsView fresh = new()
        {
            SeriesCount = 11,
            SampleCount = 21,
            SubSampleCount = 31,
            ApmCount = 41,
            ParticleCount = 51,
        };
        IGridResultCache cache = Substitute.For<IGridResultCache>();
        cache
            .TryGetAsync<SeriesCountsView>(Arg.Any<string>())
            .Returns(Task.FromResult(new GridCacheHit<SeriesCountsView>(true, [cached], 1)));
        TestContext.Services.AddSingleton(cache);

        TaskCompletionSource<SeriesCountsView> response = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Api.GetSeriesCounts(Arg.Any<DataQuery>()).Returns(response.Task);

        IRenderedComponent<SeriesCountsGrid> component = TestContext.Render<SeriesCountsGrid>();
        Task refresh = null!;
        await component.InvokeAsync(() =>
        {
            refresh = component.Instance.RefreshSummaryData(new DataQuery());
        });

        component.WaitForAssertion(() =>
        {
            RadzenDataGrid<SeriesCountsView> grid = component
                .FindComponent<RadzenDataGrid<SeriesCountsView>>()
                .Instance;
            Assert.True(grid.IsLoading);
            Assert.Equal(10, Assert.Single(grid.Data ?? []).SeriesCount);
            Assert.Equal(
                "true",
                component.Find(".ne-series-counts-grid").GetAttribute("aria-busy")
            );
        });

        response.SetResult(fresh);
        await refresh;

        component.WaitForAssertion(() =>
        {
            RadzenDataGrid<SeriesCountsView> grid = component
                .FindComponent<RadzenDataGrid<SeriesCountsView>>()
                .Instance;
            Assert.False(grid.IsLoading);
            Assert.Equal(11, Assert.Single(grid.Data ?? []).SeriesCount);
            Assert.Equal(
                "false",
                component.Find(".ne-series-counts-grid").GetAttribute("aria-busy")
            );
        });
    }
}
