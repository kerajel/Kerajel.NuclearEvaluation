using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Workbench;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Tests;

public class WorkbenchQueryResultsLoadingTests : TestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RerunningPresetKeepsPreviousRowsUntilTheRequestSettles(bool fails)
    {
        using QueryHandler handler = new();
        using HttpClient http = new(handler) { BaseAddress = new Uri("https://localhost") };
        TestContext.Services.AddSingleton(http);
        IRenderedComponent<WorkbenchQueryBuilder> builder =
            TestContext.Render<WorkbenchQueryBuilder>(parameters =>
                parameters.Add(component => component.RunExampleOnLoad, true)
            );
        SiteAssistantWorkspace assistant =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        builder.WaitForAssertion(() =>
            Assert.Contains("Previous series", builder.Find("tbody").TextContent)
        );
        SiteAssistantTarget previousTarget = Assert.IsType<SiteAssistantTarget>(assistant.Current);

        Task rerun = builder
            .Find(".wbq-example")
            .TriggerEventAsync("onclick", new MouseEventArgs());

        builder.WaitForAssertion(() =>
        {
            Assert.Contains("Previous series", builder.Find("tbody").TextContent);
            Assert.Equal("true", builder.Find(".ne-grid-refresh").GetAttribute("aria-busy"));
            Assert.True(builder.Find("table").HasAttribute("inert"));
            Assert.Null(assistant.Current);
        });

        TaskCompletionSource<SiteAssistantTarget> newTarget = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        assistant.Changed += TrackNewTarget;
        handler.Replacement.SetResult(
            fails ? new(HttpStatusCode.InternalServerError) : QueryPage("New series")
        );
        await rerun.WaitAsync(DefaultWaitForStateTimeout);
        if (fails)
        {
            builder.WaitForAssertion(() =>
            {
                Assert.Contains(
                    "could not be completed",
                    builder.Find(".wb-query-result-error").TextContent
                );
                Assert.Empty(builder.FindAll("table"));
                Assert.Null(assistant.Current);
            });
            await builder
                .Find(".wb-query-result-error button")
                .TriggerEventAsync("onclick", new MouseEventArgs());
        }

        builder.WaitForAssertion(() =>
        {
            Assert.Contains("New series", builder.Find("tbody").TextContent);
            Assert.DoesNotContain("Previous series", builder.Find("tbody").TextContent);
            Assert.Equal("false", builder.Find(".ne-grid-refresh").GetAttribute("aria-busy"));
            Assert.False(builder.Find("table").HasAttribute("inert"));
        });
        Assert.NotSame(previousTarget, await newTarget.Task.WaitAsync(DefaultWaitForStateTimeout));
        assistant.Changed -= TrackNewTarget;

        void TrackNewTarget()
        {
            if (assistant.Current is { } target)
                newTarget.TrySetResult(target);
        }
    }

    static HttpResponseMessage QueryPage(string name) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new WorkbenchQueryPage([new() { SeriesName = name }], 1)),
        };

    sealed class QueryHandler : HttpMessageHandler
    {
        int _requests;
        public TaskCompletionSource<HttpResponseMessage> Replacement { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            ++_requests switch
            {
                1 => Task.FromResult(QueryPage("Previous series")),
                2 => Replacement.Task.WaitAsync(cancellationToken),
                _ => Task.FromResult(QueryPage("New series")),
            };
    }
}
