using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Client.Shared.Workbench;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Tests;

public class ImportReviewTests : TestBase
{
    [Fact]
    public void OpensFullArchiveDirectlyAndLoadsTheAnnualSampleOnDemand()
    {
        using ReviewHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost") };
        TestContext.Services.AddSingleton(http);
        IRenderedComponent<ImportReview> review = TestContext.Render<ImportReview>(parameters =>
            parameters.Add(component => component.Ready, false)
        );
        Assert.Equal("Full archive", review.Find(".ir-view [aria-pressed='true']").TextContent);
        Assert.Null(handler.Query);

        review.Render(parameters =>
            parameters
                .Add(component => component.Ready, true)
                .Add(component => component.ObservationCount, 10_000_005)
        );
        review.WaitForAssertion(() =>
        {
            Assert.Contains(
                "ALBANY_2025.csv",
                review.Find("tbody").TextContent,
                StringComparison.Ordinal
            );
        });
        Assert.Equal(new RadNetQuery(RadNetEntity.Observation, Skip: 0, Top: 50), handler.Query);
        Assert.False(handler.AnnualSampleRequested);

        review
            .FindAll(".ir-view button")
            .Single(button => button.TextContent == "Annual CSV")
            .Click();
        review.WaitForAssertion(() =>
        {
            Assert.Contains(
                "COLORADO_SPRINGS_2025.csv",
                review.Find(".ir-source").TextContent,
                StringComparison.Ordinal
            );
            Assert.False(review.Find(".ir-intake-controls .ir-primary").HasAttribute("disabled"));
        });
        Assert.True(handler.AnnualSampleRequested);
    }

    sealed class ReviewHandler : HttpMessageHandler
    {
        public RadNetQuery? Query { get; private set; }
        public bool AnnualSampleRequested { get; private set; }

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
                    AnnualSampleRequested = true;
                    content = JsonContent.Create(
                        new ImportReviewAvailability(
                            new(
                                1,
                                "CO",
                                "Colorado Springs",
                                2025,
                                "COLORADO_SPRINGS_2025.csv",
                                "",
                                1,
                                "Colorado Springs"
                            ),
                            true
                        )
                    );
                    break;
                case "/api/radnet/query":
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
