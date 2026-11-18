using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Client.Shared.Workbench;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Tests;

public class ImportReviewLargeCsvTests : TestBase
{
    [Fact]
    public async Task StagingCsvCollectionLoadsOnlyTheFirstPageFromTheStagedDataset()
    {
        using LargeCsvHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost") };
        TestContext.Services.AddSingleton(http);
        IRenderedComponent<ImportReview> review = TestContext.Render<ImportReview>();
        Assert.Equal("Annual CSV", review.Find(".ir-view [aria-pressed='true']").TextContent);

        review
            .FindAll(".ir-view button")
            .Single(button => button.TextContent == "CSV collection")
            .Click();
        Task staging = review
            .Find(".ir-intake-controls .ir-primary")
            .TriggerEventAsync("onclick", new MouseEventArgs());
        review.WaitForAssertion(() =>
            Assert.Equal(
                "Preparing the CSV collection in DuckDB…",
                review.Find("[role='status']").TextContent
            )
        );
        Assert.Null(handler.Query);

        handler.Stage.SetResult(new("EPA RadNet 2016–2025", 640L * 1_048_576, 10_000_005, 1_386));
        await staging.WaitAsync(DefaultWaitForStateTimeout);
        review.WaitForAssertion(() =>
        {
            string details = review
                .Find("[aria-label='Staged CSV collection details']")
                .TextContent;
            Assert.Contains("10,000,005", details, StringComparison.Ordinal);
            Assert.Contains("640.0 MiB", details, StringComparison.Ordinal);
            Assert.Contains("1,386", details, StringComparison.Ordinal);
            Assert.Contains(
                "ALBANY_2025.csv",
                review.Find("tbody").TextContent,
                StringComparison.Ordinal
            );
        });
        Assert.Equal(new RadNetQuery(RadNetEntity.Observation, Skip: 0, Top: 50), handler.Query);
    }

    sealed class LargeCsvHandler : HttpMessageHandler
    {
        public TaskCompletionSource<ImportReviewLargeStage> Stage { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RadNetQuery? Query { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            HttpContent content;
            switch (request.RequestUri?.AbsolutePath)
            {
                case "/api/workbench/import-review":
                    Assert.Equal(HttpMethod.Get, request.Method);
                    content = JsonContent.Create(new ImportReviewAvailability(null, false, true));
                    break;
                case "/api/workbench/import-review/large/stage":
                    Assert.Equal(HttpMethod.Post, request.Method);
                    content = JsonContent.Create(await Stage.Task.WaitAsync(ct));
                    break;
                case "/api/workbench/import-review/large/query":
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Query = await request.Content!.ReadFromJsonAsync<RadNetQuery>(ct);
                    content = JsonContent.Create(
                        new RadNetQueryResult(
                            [
                                new()
                                {
                                    StationName = "Albany",
                                    FileName = "ALBANY_2025.csv",
                                    SourceRow = 20,
                                },
                            ],
                            10_000_005
                        )
                    );
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected request to {request.RequestUri}."
                    );
            }

            return new(HttpStatusCode.OK) { Content = content };
        }
    }
}
