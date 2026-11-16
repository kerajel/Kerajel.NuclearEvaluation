using NuclearEvaluation.Server.Services.Assistant;

namespace NuclearEvaluation.Server.Tests;

public class SeriesSchemaCacheTests
{
    static readonly SeriesSchemaDescription Schema = new(
        [
            new SeriesEntityDescription(
                "Series",
                "Series",
                [new("Id", "Int32", false, [], ["Equal"])]
            ),
        ],
        ["GreaterOrEqual"]
    );

    [Fact]
    public async Task ConcurrentColdCallsShareTheFetchedSchema()
    {
        using AssistantCatalogCache<SeriesSchemaDescription> cache = new();
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
        TaskCompletionSource fetchStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<SeriesSchemaDescription> fetchResult = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        int fetches = 0;

        Task<SeriesSchemaDescription> FetchAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref fetches);
            fetchStarted.TrySetResult();
            return fetchResult.Task.WaitAsync(ct);
        }

        Task<SeriesSchemaDescription> first = cache.GetAsync(FetchAsync, deadline.Token);
        await fetchStarted.Task.WaitAsync(deadline.Token);
        Task<SeriesSchemaDescription> second = cache.GetAsync(FetchAsync, deadline.Token);
        fetchResult.SetResult(Schema);

        Assert.All(await Task.WhenAll(first, second), schema => Assert.Same(Schema, schema));
        Assert.Same(Schema, await cache.GetAsync(FetchAsync, deadline.Token));
        Assert.Equal(1, Volatile.Read(ref fetches));
    }

    [Fact]
    public async Task FailedFetchCanBeRetriedAndSuccessfulResultIsCached()
    {
        using AssistantCatalogCache<SeriesSchemaDescription> cache = new();
        InvalidOperationException failure = new("MCP schema fetch failed.");
        int fetches = 0;

        Task<SeriesSchemaDescription> FetchAsync(CancellationToken _) =>
            ++fetches == 1
                ? Task.FromException<SeriesSchemaDescription>(failure)
                : Task.FromResult(Schema);

        Assert.Same(
            failure,
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                cache.GetAsync(FetchAsync, CancellationToken.None)
            )
        );
        Assert.Same(Schema, await cache.GetAsync(FetchAsync, CancellationToken.None));
        Assert.Same(Schema, await cache.GetAsync(FetchAsync, CancellationToken.None));
        Assert.Equal(2, fetches);
    }
}
