using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Client.Pages;
using NuclearEvaluation.Client.Shared.Workbench;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Tests;

public class WorkbenchLoadingTests : TestBase
{
    [Fact]
    public async Task FirstOpenShowsTheGridPlaceholderThroughCatalogAndSeriesRequests()
    {
        using WorkbenchHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost") };
        TestContext.Services.AddSupplyValueFromQueryProvider();
        TestContext.Services.AddSingleton(http);

        IRenderedComponent<Workbench> page = TestContext.Render<Workbench>();
        IRenderedComponent<WorkbenchSeriesGrid> series = page.FindComponent<WorkbenchSeriesGrid>();
        Assert.Equal("true", page.Find(".wb-coverage").GetAttribute("aria-busy"));
        Assert.Equal(
            "Loading series…",
            series.Find(".wb-loading-grid [role='status']").TextContent
        );
        Assert.False(handler.SeriesRequested.Task.IsCompleted);

        DateOnly first = new(2025, 1, 1);
        DateOnly last = new(2025, 12, 31);
        handler.Catalog.SetResult(
            new(
                new(true, 1, 1, 100, [], new Dictionary<string, string>()),
                [new("CA", "Fresno", first, last, 100, 1)]
            )
        );
        await handler.SeriesRequested.Task.WaitAsync(DefaultWaitForStateTimeout);
        Assert.Equal(
            "Loading series…",
            series.Find(".wb-loading-grid [role='status']").TextContent
        );
        Assert.DoesNotContain("No series match", series.Markup, StringComparison.Ordinal);

        handler.Series.SetResult(
            new(
                [
                    new(
                        Guid.NewGuid(),
                        "Fresno 2025",
                        "CA",
                        "Fresno",
                        first,
                        last,
                        WorkbenchReview.Reviewed,
                        "",
                        100,
                        1
                    ),
                ],
                1,
                100
            )
        );
        series.WaitForAssertion(() =>
        {
            Assert.Empty(series.FindAll(".wb-loading-grid"));
            Assert.Contains(
                "Fresno 2025",
                series.Find("tbody").TextContent,
                StringComparison.Ordinal
            );
        });
    }

    sealed class WorkbenchHandler : HttpMessageHandler
    {
        public TaskCompletionSource<WorkbenchCatalog> Catalog { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SeriesRequested { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<WorkbenchSeriesPage> Series { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            HttpContent content;
            if (request.RequestUri?.AbsolutePath == "/api/workbench")
            {
                content = JsonContent.Create(await Catalog.Task.WaitAsync(ct));
            }
            else
            {
                Assert.Equal("/api/workbench/series/query", request.RequestUri?.AbsolutePath);
                SeriesRequested.SetResult();
                content = JsonContent.Create(await Series.Task.WaitAsync(ct));
            }

            return new(HttpStatusCode.OK) { Content = content };
        }
    }
}
