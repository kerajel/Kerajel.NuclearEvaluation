using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Client.Tests;

public class SeriesCountsGridTests : TestBase
{
    [Fact]
    public void InitialRenderIsBusyAndDoesNotShowAnEmptyResult()
    {
        IRenderedComponent<SeriesCountsGrid> component = TestContext.Render<SeriesCountsGrid>();

        Assert.Equal("true", component.Find(".ne-series-counts-grid").GetAttribute("aria-busy"));
        Assert.Contains("Updating totals", component.Find("[role=status]").TextContent);
        Assert.All(component.FindAll("dd"), total => Assert.Equal("—", total.TextContent));
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
            Assert.Equal("10", component.FindAll("dd")[0].TextContent);
            Assert.Equal(
                "true",
                component.Find(".ne-series-counts-grid").GetAttribute("aria-busy")
            );
        });

        response.SetResult(fresh);
        await refresh;

        component.WaitForAssertion(() =>
        {
            Assert.Equal("11", component.FindAll("dd")[0].TextContent);
            Assert.Equal(
                "false",
                component.Find(".ne-series-counts-grid").GetAttribute("aria-busy")
            );
        });
    }

    [Fact]
    public async Task FailedRefreshDoesNotShowTotalsFromAnotherFilter()
    {
        DataQuery first = new() { Filter = "Id == 1" };
        DataQuery nextPage = new()
        {
            Filter = first.Filter,
            Skip = 25,
            Top = 25,
        };
        DataQuery second = new() { Filter = "Id == 2" };
        Api.GetSeriesCounts(first)
            .Returns(Task.FromResult(new SeriesCountsView { SeriesCount = 1 }));
        Api.GetSeriesCounts(nextPage)
            .Returns(Task.FromException<SeriesCountsView>(new HttpRequestException()));
        TaskCompletionSource<SeriesCountsView> response = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Api.GetSeriesCounts(second).Returns(response.Task);
        IRenderedComponent<SeriesCountsGrid> component = TestContext.Render<SeriesCountsGrid>();

        await component.InvokeAsync(() => component.Instance.RefreshSummaryData(first));
        Assert.Equal("1", component.FindAll("dd")[0].TextContent);

        await component.InvokeAsync(() => component.Instance.RefreshSummaryData(nextPage));
        Assert.Equal("1", component.FindAll("dd")[0].TextContent);

        Task refresh = null!;
        await component.InvokeAsync(() =>
        {
            refresh = component.Instance.RefreshSummaryData(second);
        });
        Assert.Equal("true", component.Find(".ne-series-counts-grid").GetAttribute("aria-busy"));
        Assert.Equal("1", component.FindAll("dd")[0].TextContent);

        response.SetException(new HttpRequestException());
        await refresh;

        Assert.Contains(
            "Totals could not be refreshed",
            component.Find("[role=alert]").TextContent
        );
        Assert.All(component.FindAll("dd"), total => Assert.Equal("—", total.TextContent));
    }
}
