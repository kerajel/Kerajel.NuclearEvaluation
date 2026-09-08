using System.Net;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Tests;

public class ApiClientTests
{
    sealed class Handler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        ) => send(request, ct);
    }

    [Fact]
    public async Task FailedCountsRequestIsNotRenderedAsZeroTotals()
    {
        using HttpClient http = new(
            new Handler(
                (_, _) =>
                    Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.Forbidden)
                        {
                            Content = new StringContent("{\"error\":\"captcha_required\"}"),
                        }
                    )
            )
        )
        {
            BaseAddress = new Uri("https://example.test"),
        };
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new NuclearEvaluationApiClient(http).GetSeriesCounts(new())
        );
    }

    [Fact]
    public async Task CanceledRequestPropagatesCancellation()
    {
        using CancellationTokenSource cts = new();
        using HttpClient http = new(
            new Handler(
                async (_, ct) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                    return new HttpResponseMessage();
                }
            )
        )
        {
            BaseAddress = new Uri("https://example.test"),
        };
        Task<DataResult<NuclearEvaluation.Shared.Models.Views.SeriesView>> pending =
            new NuclearEvaluationApiClient(http).GetSeriesViews(new(), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
