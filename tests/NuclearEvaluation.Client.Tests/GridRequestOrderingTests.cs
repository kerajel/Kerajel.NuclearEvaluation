using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;

namespace NuclearEvaluation.Client.Tests;

public class GridRequestOrderingTests : TestBase
{
    public sealed class TestGrid : BaseGridGeneric<SeriesView>
    {
        public override string EntityDisplayName => "Test";

        public override Task Reset(bool resetColumnState = true, bool resetRowState = false) =>
            Task.CompletedTask;

        public override Task LoadData(LoadDataArgs args) => Task.CompletedTask;

        public IReadOnlyList<SeriesView> Rows => entries;

        public Task<bool> Fetch(DataQuery query, Func<Task<DataResult<SeriesView>>> fetch) =>
            FetchData(query, fetch);
    }

    [Fact]
    public async Task SlowCacheReadCannotOverwriteANewerQuery()
    {
        IGridResultCache cache = Substitute.For<IGridResultCache>();
        TaskCompletionSource<GridCacheHit<SeriesView>> delayed = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        cache
            .TryGetAsync<SeriesView>(Arg.Any<string>())
            .Returns(Task.FromResult(new GridCacheHit<SeriesView>(false, [], 0)));
        cache
            .TryGetAsync<SeriesView>(Arg.Is<string>(key => key.Contains("old")))
            .Returns(delayed.Task);
        TestContext.Services.AddSingleton(cache);
        IRenderedComponent<TestGrid> component = TestContext.Render<TestGrid>();
        bool oldFetched = false;
        Task<bool> oldRequest = null!;
        await component.InvokeAsync(() =>
        {
            oldRequest = component.Instance.Fetch(
                new() { Filter = "old" },
                () =>
                {
                    oldFetched = true;
                    return Task.FromResult(DataResult<SeriesView>.Succeeded([new() { Id = 1 }], 1));
                }
            );
        });
        await component.InvokeAsync(() =>
            component.Instance.Fetch(
                new() { Filter = "new" },
                () => Task.FromResult(DataResult<SeriesView>.Succeeded([new() { Id = 2 }], 1))
            )
        );
        delayed.SetResult(new(true, [new() { Id = 1 }], 1));
        Assert.False(await oldRequest);
        Assert.False(oldFetched);
        Assert.Equal(2, Assert.Single(component.Instance.Rows).Id);
    }

    [Fact]
    public async Task SlowApiResponseCannotPublishStaleRows()
    {
        IRenderedComponent<TestGrid> component = TestContext.Render<TestGrid>();
        TaskCompletionSource<DataResult<SeriesView>> delayed = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Task<bool> oldRequest = null!;
        await component.InvokeAsync(() =>
        {
            oldRequest = component.Instance.Fetch(new() { Filter = "old" }, () => delayed.Task);
        });
        await component.InvokeAsync(() =>
            component.Instance.Fetch(
                new() { Filter = "new" },
                () => Task.FromResult(DataResult<SeriesView>.Succeeded([new() { Id = 2 }], 1))
            )
        );
        delayed.SetResult(DataResult<SeriesView>.Succeeded([new() { Id = 1 }], 1));
        Assert.False(await oldRequest);
        Assert.Equal(2, Assert.Single(component.Instance.Rows).Id);
    }
}
