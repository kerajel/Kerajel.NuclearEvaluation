using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Client.Shared.Radiochemistry;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Tests;

public class RemDbResultsLoadingTests : TestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreservesRowsDuringScopeChangeAndOnlyCommitsSuccessfulResponse(bool fails)
    {
        using ResultHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost") };
        TestContext.Services.AddSingleton(http);

        IRenderedComponent<RemDbResults> grid = TestContext.Render<RemDbResults>(parameters =>
            parameters.Add(component => component.Query, new(RemDbEntity.Site))
        );
        Assert.Equal(
            "Loading sites…",
            grid.Find(".ne-grid-placeholder [role='status']").TextContent
        );
        handler.Initial.SetResult(Page(new() { SiteName = "Previous site" }));
        grid.WaitForAssertion(() =>
            Assert.Contains("Previous site", grid.Find("tbody").TextContent)
        );

        grid.Render(parameters =>
            parameters.Add(component => component.Query, new(RemDbEntity.Measurement))
        );
        Assert.Contains("Previous site", grid.Find("tbody").TextContent);
        Assert.Equal("true", grid.Find(".ne-grid-refresh").GetAttribute("aria-busy"));
        Assert.True(grid.Find("table").HasAttribute("inert"));

        handler.Replacement.SetResult(
            fails ? new(HttpStatusCode.ServiceUnavailable) : Page(new() { Nuclide = "I-131" })
        );
        if (fails)
        {
            grid.WaitForAssertion(() =>
            {
                Assert.Empty(grid.FindAll("table"));
                Assert.Contains("could not be completed", grid.Find("[role='alert']").TextContent);
            });
            grid.Find("[role='alert'] button").Click();
        }

        grid.WaitForAssertion(() =>
        {
            Assert.Contains("I-131", grid.Find("tbody").TextContent);
            Assert.DoesNotContain("Previous site", grid.Find("tbody").TextContent);
            Assert.Equal("false", grid.Find(".ne-grid-refresh").GetAttribute("aria-busy"));
            Assert.False(grid.Find("table").HasAttribute("inert"));
        });
    }

    static HttpResponseMessage Page(RemDbRow row) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(new RemDbQueryResult([row], 1)) };

    sealed class ResultHandler : HttpMessageHandler
    {
        int _requests;
        public TaskCompletionSource<HttpResponseMessage> Initial { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<HttpResponseMessage> Replacement { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            ++_requests switch
            {
                1 => Initial.Task.WaitAsync(cancellationToken),
                2 => Replacement.Task.WaitAsync(cancellationToken),
                _ => Task.FromResult(Page(new() { Nuclide = "I-131" })),
            };
    }
}
