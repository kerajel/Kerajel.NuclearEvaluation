using System.Net;
using System.Net.Http.Headers;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using NuclearEvaluation.Client.Pages;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared;
using NuclearEvaluation.Client.Shared.Radiation;
using NuclearEvaluation.Client.Shared.Workbench;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Client.Tests;

public class SiteAssistantPanelTests : TestBase
{
    static readonly SiteAssistantContext Projects = new(
        AssistantPage.Evaluation,
        AssistantSurface.Projects
    );
    const string ProviderUnavailable = "The AI service is unavailable right now. Try again later.";

    [Theory]
    [InlineData(HttpStatusCode.OK, true, true)]
    [InlineData(HttpStatusCode.OK, false, false)]
    [InlineData(HttpStatusCode.Forbidden, true, false)]
    [InlineData(HttpStatusCode.NotFound, true, false)]
    public void HonorsAssistantAvailabilityOnPublicSites(
        HttpStatusCode availabilityStatus,
        bool chatAvailable,
        bool launcherVisible
    )
    {
        using AssistantHandler handler = new(
            availabilityStatus: availabilityStatus,
            chatAvailable: chatAvailable
        );
        using HttpClient http = Register(handler, "https://nuclearevaluation.example/");

        IRenderedComponent<SiteAssistantPanel> panel = TestContext.Render<SiteAssistantPanel>();

        Assert.Equal(1, handler.StatusRequests);
        Assert.Equal(
            launcherVisible ? 1 : 0,
            panel.FindAll("button[aria-controls='site-assistant-panel']").Count
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AppliesActionToTheGridCapturedByTheQuestion(bool scrollHelperUnavailable)
    {
        if (scrollHelperUnavailable)
            TestContext
                .JSInterop.SetupVoid("scrollSiteAssistantLog", _ => true)
                .SetException(new JSException("The scroll helper is unavailable."));

        AssistantAction action = new(AssistantActionKind.FilterGrid)
        {
            GridQuery = new(
                AssistantSurface.Projects,
                [new("Name", SeriesComparison.Contains, "sandbox")],
                "Id",
                true,
                3
            ),
        };
        using AssistantHandler handler = new();
        handler.Reply.SetResult(new SiteAssistantReply("Updated the current grid.", action));
        using HttpClient http = Register(handler);
        List<AssistantAction> applied = [];
        RegisterProjects(applied);
        IRenderedComponent<SiteAssistantPanel> panel = TestContext.Render<SiteAssistantPanel>();
        panel.WaitForElement("aside button").Click();
        panel.Find("textarea").Input("Show three sandbox projects, highest ID first");

        await panel.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);

        SiteAssistantQuestion request = await handler.Sent.Task;
        Assert.Equal(Projects, request.Context);
        Assert.Equal("Show three sandbox projects, highest ID first", request.Message);
        Assert.Equal(AssistantSurface.Projects, Assert.Single(applied).GridQuery?.Grid);
        Assert.Contains("Updated the current grid.", panel.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpensTheQueryBuilderExampleFromAnotherView()
    {
        TestContext.Services.AddSupplyValueFromQueryProvider();
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<ProjectView>.Succeeded([], 0));
        Api.GetPresetFilters(Arg.Any<CancellationToken>()).Returns([]);

        using AssistantHandler handler = new();
        handler.Reply.SetResult(
            new SiteAssistantReply(
                "Loaded the query example.",
                new(AssistantActionKind.ShowQueryBuilderExample)
            )
        );
        using HttpClient http = Register(handler);
        NavigationManager navigation = TestContext.Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/evaluation");
        SiteAssistantWorkspace workspace =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        IRenderedComponent<RetiredIsotopeRoute> evaluation =
            TestContext.Render<RetiredIsotopeRoute>(parameters =>
                parameters.AddCascadingValue(new SiteFeatures(true))
            );
        evaluation.WaitForAssertion(() => Assert.Equal(Projects, workspace.Current?.Context));

        IRenderedComponent<SiteAssistantPanel> panel = TestContext.Render<SiteAssistantPanel>();
        panel.WaitForElement("aside button").Click();
        panel.Find("textarea").Input("Show me an advanced Query Builder example");

        await panel.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);

        evaluation.WaitForAssertion(() =>
            Assert.Equal(
                new SiteAssistantContext(
                    AssistantPage.Evaluation,
                    AssistantSurface.QueryBuilder,
                    AssistantSurface.Particles
                ),
                workspace.Current?.Context
            )
        );
        Assert.Equal("/evaluation?tab=query-builder", new Uri(navigation.Uri).PathAndQuery);
        Assert.Contains(
            QueryBuilderExample.Advanced.Title,
            evaluation.Markup,
            StringComparison.Ordinal
        );
        Assert.Contains("Loaded the query example.", panel.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FiltersWorkbenchResultsWithoutChangingTheAppliedBuilderQuery()
    {
        WorkbenchQuery baseQuery = new(
            WorkbenchQueryEntity.Channel,
            [
                new(
                    WorkbenchQueryEntity.File,
                    false,
                    [new(nameof(WorkbenchQueryRow.Year), RadNetOperator.Equal, "2025")]
                ),
            ],
            ResultFilterGroups:
            [
                new(
                    nameof(WorkbenchQueryRow.Station),
                    false,
                    [new(RadNetOperator.Equal, "Fresno")]
                ),
            ]
        );
        List<WorkbenchQuery> gridQueries = [];
        using AssistantHandler handler = new(
            gridRequest: async (request, ct) =>
            {
                Assert.Equal("/api/workbench/query", request.RequestUri?.AbsolutePath);
                WorkbenchQuery? query = await request.Content!.ReadFromJsonAsync<WorkbenchQuery>(
                    ct
                );
                Assert.NotNull(query);
                gridQueries.Add(query);
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new WorkbenchQueryPage([], 0)),
                };
            }
        );
        handler.Reply.SetResult(
            new(
                "Updated the current grid.",
                new(AssistantActionKind.ApplyWorkbenchGridQuery)
                {
                    WorkbenchGridQuery = new(
                        [
                            new(
                                nameof(WorkbenchQueryRow.CountRate),
                                false,
                                [new(RadNetOperator.AtLeast, "100")]
                            ),
                        ],
                        nameof(WorkbenchQueryRow.CountRate),
                        Descending: true
                    ),
                }
            )
        );
        using HttpClient http = Register(handler);
        TestContext
            .Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/workbench?view=builder");
        IRenderedComponent<WorkbenchQueryResults> grid = TestContext.Render<WorkbenchQueryResults>(
            parameters => parameters.Add(component => component.Query, baseQuery)
        );
        grid.WaitForElement("table");
        IRenderedComponent<SiteAssistantPanel> panel = TestContext.Render<SiteAssistantPanel>();
        panel.WaitForElement("aside button").Click();
        panel.Find("textarea").Input("Show readings of at least 100 CPM, highest first");

        await panel.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);

        SiteAssistantQuestion question = await handler.Sent.Task;
        Assert.Equal(AssistantPage.Workbench, question.Context.Page);
        Assert.Equal(AssistantSurface.WorkbenchQueryResults, question.Context.Surface);
        Assert.Equal(WorkbenchQueryEntity.Channel, question.Context.WorkbenchResultEntity);
        WorkbenchQuery applied = Assert.Single(gridQueries.Skip(1));
        Assert.Equal(baseQuery.Entity, applied.Entity);
        Assert.Equal(
            Assert.Single(baseQuery.Filters!).Entity,
            Assert.Single(applied.Filters!).Entity
        );
        Assert.Equal("2025", Assert.Single(Assert.Single(applied.Filters!).Conditions).Value);
        Assert.Equal(nameof(WorkbenchQueryRow.CountRate), applied.SortField);
        Assert.True(applied.Descending);
        Assert.Equal(2, applied.ResultFilterGroups!.Count);
        Assert.Contains(applied.ResultFilterGroups, group =>
            group.Field == nameof(WorkbenchQueryRow.Station)
        );
        Assert.Contains(applied.ResultFilterGroups, group =>
            group.Field == nameof(WorkbenchQueryRow.CountRate)
        );
        Assert.NotNull(
            grid.Find("button[aria-label='Filter Count rate (CPM)'][data-active-filter='true']")
        );
        Assert.Contains("Updated the current grid.", panel.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeepsProjectObservationActionsOnTheCapturedProjectVersion(bool projectChanged)
    {
        const string route =
            "/api/workbench/projects/4eb757e3-b297-45b9-bdd6-ceda50fa5126/observations/query";
        List<(string Url, RadNetQuery Query)> gridQueries = [];
        using AssistantHandler handler = new(
            gridRequest: async (request, ct) =>
            {
                Assert.Equal(route, request.RequestUri?.AbsolutePath);
                RadNetQuery? query = await request.Content!.ReadFromJsonAsync<RadNetQuery>(ct);
                Assert.NotNull(query);
                gridQueries.Add((request.RequestUri!.PathAndQuery, query));
                return new(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new RadNetQueryResult([], 0)),
                };
            }
        );
        using HttpClient http = Register(handler);
        TestContext
            .Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/workbench?view=projects");
        IRenderedComponent<RadNetResults> grid = TestContext.Render<RadNetResults>(parameters =>
            parameters
                .Add(
                    component => component.AssistantGrid,
                    AssistantSurface.WorkbenchProjectObservations
                )
                .Add(component => component.Query, new RadNetQuery(RadNetEntity.Observation))
                .Add(component => component.QueryUrl, route + "?version=4")
        );
        grid.WaitForElement("table");
        IRenderedComponent<SiteAssistantPanel> panel = TestContext.Render<SiteAssistantPanel>();
        panel.WaitForElement("aside button").Click();
        panel.Find("textarea").Input("Show only approved observations");
        Task submission = panel.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        SiteAssistantQuestion question = await handler.Sent.Task.WaitAsync(
            DefaultWaitForStateTimeout
        );
        Assert.Equal(AssistantSurface.WorkbenchProjectObservations, question.Context.Surface);

        if (projectChanged)
            grid.Render(parameters =>
                parameters.Add(component => component.QueryUrl, route + "?version=5")
            );

        handler.Reply.SetResult(
            new(
                "Updated the current grid.",
                new(AssistantActionKind.ApplyWorkbenchGridQuery)
                {
                    WorkbenchGridQuery = new([
                        new(
                            nameof(RadNetRow.Status),
                            false,
                            [new(RadNetOperator.Equal, "APPROVED")]
                        ),
                    ]),
                }
            )
        );
        await submission.WaitAsync(DefaultWaitForStateTimeout);

        (string url, RadNetQuery query) = Assert.Single(gridQueries.Skip(1));
        if (projectChanged)
        {
            Assert.Equal(route + "?version=5", url);
            Assert.Null(query.Filters);
            Assert.Contains("The active view changed", panel.Markup, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(route + "?version=4", url);
            RadNetCondition condition = Assert.Single(Assert.Single(query.Filters!).Conditions);
            Assert.Equal(nameof(RadNetRow.Status), condition.Field);
            Assert.Equal("APPROVED", condition.Value);
            Assert.NotNull(
                grid.Find("button[aria-label='Filter Status'][data-active-filter='true']")
            );
        }
    }

    [Fact]
    public async Task DropsAnActionWhenTheActiveViewChangesDuringTheRequest()
    {
        using AssistantHandler handler = new();
        using HttpClient http = Register(handler);
        List<AssistantAction> applied = [];
        SiteAssistantWorkspace workspace = RegisterProjects(applied);
        IRenderedComponent<SiteAssistantPanel> panel = TestContext.Render<SiteAssistantPanel>();
        panel.WaitForElement("aside button").Click();
        panel.Find("textarea").Input("Clear the assistant query");
        Task submission = panel.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        await handler.Sent.Task.WaitAsync(DefaultWaitForStateTimeout);

        await panel.InvokeAsync(() =>
            workspace.Set(
                this,
                new(
                    AssistantPage.Evaluation,
                    AssistantSurface.QueryBuilder,
                    AssistantSurface.Samples
                ),
                _ => Task.FromResult(true)
            )
        );
        handler.Reply.SetResult(
            new SiteAssistantReply("Cleared.", new(AssistantActionKind.ClearGrid))
        );
        await submission;

        Assert.Empty(applied);
        Assert.Contains("The active view changed", panel.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StopsThePendingRequestAndLeavesTheGridUnchanged()
    {
        using AssistantHandler handler = new(observeCancellation: false);
        using HttpClient http = Register(handler);
        List<AssistantAction> applied = [];
        RegisterProjects(applied);
        IRenderedComponent<SiteAssistantPanel> panel = TestContext.Render<SiteAssistantPanel>();
        panel.WaitForElement("aside button").Click();
        panel.Find("textarea").Input("Show the five newest projects");
        Task submission = panel.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);
        await handler.Sent.Task.WaitAsync(DefaultWaitForStateTimeout);

        panel.WaitForElement("button[aria-label='Stop assistant request']").Click();
        handler.Reply.SetResult(
            new SiteAssistantReply("Cleared.", new(AssistantActionKind.ClearGrid))
        );
        await submission.WaitAsync(DefaultWaitForStateTimeout);

        Assert.Empty(applied);
        Assert.Contains("Request stopped.", panel.Markup, StringComparison.Ordinal);
        panel.Find("textarea").Input("Show three projects");
        Assert.False(panel.Find("button[aria-label='Send question']").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, ProviderUnavailable, null)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ProviderUnavailable, null)]
    [InlineData(HttpStatusCode.GatewayTimeout, ProviderUnavailable, null)]
    [InlineData(
        HttpStatusCode.BadRequest,
        "The assistant could not complete the request. Try again.",
        null
    )]
    [InlineData(
        HttpStatusCode.TooManyRequests,
        "Assistant request limit reached. Try again in 12 seconds.",
        12
    )]
    [InlineData(
        HttpStatusCode.TooManyRequests,
        "Assistant request limit reached. Try again in 1 second.",
        1
    )]
    [InlineData(
        HttpStatusCode.TooManyRequests,
        "Assistant request limit reached. Try again in a moment.",
        null
    )]
    public async Task DisplaysFailedRepliesWithoutApplyingTheirActions(
        HttpStatusCode statusCode,
        string message,
        int? retryAfterSeconds
    )
    {
        using AssistantHandler handler = new(
            replyStatus: statusCode,
            retryAfterSeconds: retryAfterSeconds
        );
        handler.Reply.SetResult(
            new SiteAssistantReply("Cleared.", new(AssistantActionKind.ClearGrid))
        );
        using HttpClient http = Register(handler);
        List<AssistantAction> applied = [];
        RegisterProjects(applied);
        IRenderedComponent<SiteAssistantPanel> panel = TestContext.Render<SiteAssistantPanel>();
        panel.WaitForElement("aside button").Click();
        panel.Find("textarea").Input("Clear the assistant query");

        await panel.Find("form").TriggerEventAsync("onsubmit", EventArgs.Empty);

        Assert.Equal(
            $"Assistant: {message}",
            panel.Find(".site-assistant__reply").TextContent.Trim()
        );
        Assert.Empty(applied);
    }

    HttpClient Register(AssistantHandler handler, string baseAddress = "http://127.0.0.1:8081")
    {
        HttpClient http = new(handler) { BaseAddress = new Uri(baseAddress) };
        TestContext.Services.AddSingleton(http);
        return http;
    }

    SiteAssistantWorkspace RegisterProjects(List<AssistantAction> applied)
    {
        TestContext.Services.GetRequiredService<NavigationManager>().NavigateTo("/evaluation");
        SiteAssistantWorkspace workspace =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        workspace.Set(this, Projects, action =>
        {
            applied.Add(action);
            return Task.FromResult(true);
        });
        return workspace;
    }

    sealed class AssistantHandler(
        bool observeCancellation = true,
        HttpStatusCode replyStatus = HttpStatusCode.OK,
        int? retryAfterSeconds = null,
        HttpStatusCode availabilityStatus = HttpStatusCode.OK,
        bool chatAvailable = true,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? gridRequest = null
    ) : HttpMessageHandler
    {
        public int StatusRequests { get; private set; }
        public TaskCompletionSource<SiteAssistantQuestion> Sent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<SiteAssistantReply> Reply { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            if (
                request.RequestUri?.AbsolutePath.StartsWith(
                    "/api/workbench/",
                    StringComparison.Ordinal
                ) == true
                && gridRequest is not null
            )
                return await gridRequest(request, ct);

            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/api/assistant/status", request.RequestUri?.AbsolutePath);
                StatusRequests++;
                return new HttpResponseMessage(availabilityStatus)
                {
                    Content = JsonContent.Create(new AssistantStatus(chatAvailable)),
                };
            }

            Assert.Equal("/api/assistant/chat", request.RequestUri?.AbsolutePath);
            Assert.NotNull(request.Content);
            SiteAssistantQuestion? question =
                await request.Content.ReadFromJsonAsync<SiteAssistantQuestion>(ct);
            Sent.SetResult(
                question ?? throw new InvalidOperationException("Missing assistant request.")
            );
            HttpResponseMessage response = new(replyStatus)
            {
                Content = JsonContent.Create(
                    await Reply.Task.WaitAsync(observeCancellation ? ct : CancellationToken.None)
                ),
            };
            if (retryAfterSeconds is int seconds)
                response.Headers.RetryAfter = new RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(seconds)
                );
            return response;
        }
    }
}
