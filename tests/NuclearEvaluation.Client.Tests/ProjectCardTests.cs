using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NuclearEvaluation.Client.Pages;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Generics;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;
using Shouldly;

namespace NuclearEvaluation.Client.Tests;

public class ProjectCardTests : TestBase
{
    static ProjectView Project(int id, string name) => new() { Id = id, Name = name };

    void SetupProject(ProjectView project)
    {
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<ProjectView>.Succeeded([project], 1));
    }

    IRenderedComponent<ProjectCard> RenderProjectCard(ProjectView project) =>
        TestContext.Render<ProjectCard>(parameters =>
            parameters
                .Add(p => p.Id, project.Id)
                .Add(p => p.TabRenderMode, Radzen.TabRenderMode.Server)
        );

    [Fact]
    public void Render_ShouldRenderProjectName()
    {
        ProjectView project = Project(1, "Plutonium assessment test");
        SetupProject(project);

        IRenderedComponent<ProjectCard> component = RenderProjectCard(project);

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#projectNameHeading").TextContent.ShouldBe(project.Name);
    }

    [Fact]
    public void RecordPathOpensSamplesForTheCurrentProject()
    {
        ProjectView project = Project(12, "Project data");
        project.SampleCount = 42;
        SetupProject(project);
        SiteAssistantWorkspace workspace =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        IRenderedComponent<ProjectCard> component = RenderProjectCard(project);

        IElement samples = component
            .FindAll(".project-records__path button")
            .Single(button => button.QuerySelector("span")?.TextContent == "Samples");
        samples.QuerySelector("strong")!.TextContent.ShouldBe("42");
        samples.Click();

        component.WaitForAssertion(() =>
        {
            component.Find("[role=tab][aria-selected=true]").TextContent.Trim().ShouldBe("Samples");
            workspace.Current!.Context.Surface.ShouldBe(AssistantSurface.Samples);
            _ = Api.Received()
                .GetSampleViews(
                    Arg.Is<DataQuery>(query => query.ProjectId == project.Id),
                    Arg.Any<CancellationToken>()
                );
        });
    }

    [Fact]
    public async Task AssistantActionUsesTheSelectedProjectGridAndRejectsAnOldTab()
    {
        ProjectView project = Project(12, "Project data");
        SetupProject(project);
        SiteAssistantWorkspace workspace =
            TestContext.Services.GetRequiredService<SiteAssistantWorkspace>();
        IRenderedComponent<ProjectCard> component = RenderProjectCard(project);

        component.FindAll("[role=tab]").Single(tab => tab.TextContent.Trim() == "Samples").Click();
        component.WaitForAssertion(() =>
            workspace.Current!.Context.Surface.ShouldBe(AssistantSurface.Samples)
        );
        SiteAssistantTarget target = workspace.Current!;
        AssistantGridQuery query = new(
            AssistantSurface.Samples,
            [],
            nameof(SampleView.Id),
            true,
            5
        );
        AssistantAction action = new(AssistantActionKind.FilterGrid) { GridQuery = query };
        await component.InvokeAsync(async () => (await target.ApplyAsync(action)).ShouldBeTrue());
        await Api.Received()
            .GetSampleViews(
                Arg.Is<DataQuery>(data =>
                    data.ProjectId == project.Id && data.AssistantQuery == query
                ),
                Arg.Any<CancellationToken>()
            );

        component.FindAll("[role=tab]").Single(tab => tab.TextContent.Trim() == "Overview").Click();
        component.WaitForAssertion(() =>
            workspace.Current!.Context.Surface.ShouldBe(AssistantSurface.None)
        );
        await component.InvokeAsync(async () => (await target.ApplyAsync(action)).ShouldBeFalse());
    }

    [Fact]
    public void RenameProject_WhenValidInput_ShouldUpdateProjectName()
    {
        ProjectView project = Project(1, "Initial Project Name");
        const string newProjectName = "Updated Project Name";
        SetupProject(project);
        Api.IsProjectNameAvailable(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(true);

        IRenderedComponent<ProjectCard> component = RenderProjectCard(project);

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#editProjectNameButton").Click();
        component.WaitForState(
            () => component.Instance._isEditingProjectName,
            DefaultWaitForStateTimeout
        );

        component.Find("#projectNameInput").Input(newProjectName);
        component.WaitForAssertion(
            () => component.Find("#saveProjectNameButton").IsDisabled().ShouldBeFalse(),
            DefaultWaitForStateTimeout
        );
        component.Find("#saveProjectNameButton").Click();

        component.WaitForState(
            () => !component.Instance._isEditingProjectName,
            DefaultWaitForStateTimeout
        );

        component.Find("#projectNameHeading").TextContent.ShouldBe(newProjectName);
        Api.Received()
            .UpdateProjectField(
                Arg.Is<ProjectFieldUpdate>(u =>
                    u.Field == ProjectField.Name && u.StringValue == newProjectName
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public void RenameProject_WhenNameAlreadyExists_ShouldInvalidateInput()
    {
        ProjectView project = Project(1, "ProjectNameA");
        SetupProject(project);
        Api.IsProjectNameAvailable(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(false);

        IRenderedComponent<ProjectCard> component = RenderProjectCard(project);

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#editProjectNameButton").Click();
        component.WaitForState(
            () => component.Instance._isEditingProjectName,
            DefaultWaitForStateTimeout
        );

        component.Find("#projectNameInput").Input("ProjectNameB");

        component.WaitForAssertion(
            () =>
            {
                IElement saveButton = component.Find("#saveProjectNameButton");
                saveButton.IsDisabled().ShouldBeTrue();

                IElement validationLabel = component.Find(".validation-tooltip");
                validationLabel.TextContent.ShouldBe("Name is already in use");
            },
            timeout: TimeSpan.FromSeconds(10)
        );
    }
}

public class ProjectNavigationTests : TestBase
{
    [Fact]
    public void NavigatingBetweenProjectIdsReloadsTheExistingComponent()
    {
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                int id = call.Arg<DataQuery>().Filter!.Contains("2") ? 2 : 1;
                return DataResult<ProjectView>.Succeeded(
                    [new() { Id = id, Name = $"Project number {id}" }],
                    1
                );
            });
        IRenderedComponent<ProjectCard> component = TestContext.Render<ProjectCard>(p =>
            p.Add(x => x.Id, 1)
        );
        component.WaitForAssertion(() => Assert.Contains("Project number 1", component.Markup));
        component.Render(p => p.Add(x => x.Id, 2));
        component.WaitForAssertion(() => Assert.Contains("Project number 2", component.Markup));
        Assert.DoesNotContain("Project number 1", component.Markup);
    }

    [Fact]
    public async Task OlderVisitToSameProjectDoesNotClearLoadingForTheCurrentVisit()
    {
        TaskCompletionSource<DataResult<ProjectView>> firstVisitResult = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource<DataResult<ProjectView>> latestVisitResult = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource firstVisitStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource latestVisitStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        int visits = 0;
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                call.Arg<DataQuery>().Filter == "Id == 3"
                    ? ProjectResult(3, "Intervening project")
                    : ProjectResult(1, "Starting project")
            );
        Api.GetProjectViews(
                Arg.Is<DataQuery>(query => query.Filter == "Id == 2"),
                Arg.Any<CancellationToken>()
            )
            .Returns(_ =>
            {
                if (++visits == 1)
                {
                    firstVisitStarted.SetResult();
                    return firstVisitResult.Task;
                }

                latestVisitStarted.SetResult();
                return latestVisitResult.Task;
            });
        IRenderedComponent<ProjectCard> component = TestContext.Render<ProjectCard>(p =>
            p.Add(x => x.Id, 1)
        );

        Task EnterProject(int id) =>
            component.InvokeAsync(() =>
                component.Instance.SetParametersAsync(
                    ParameterView.FromDictionary(
                        new Dictionary<string, object?> { [nameof(ProjectCard.Id)] = id }
                    )
                )
            );

        Task firstVisit = EnterProject(2);
        await firstVisitStarted.Task.WaitAsync(DefaultWaitForStateTimeout);
        await EnterProject(3);
        Assert.Equal("Intervening project", component.Find("#projectNameHeading").TextContent);
        Task latestVisit = EnterProject(2);
        await latestVisitStarted.Task.WaitAsync(DefaultWaitForStateTimeout);

        firstVisitResult.SetResult(ProjectResult(2, "Superseded response"));
        await firstVisit;

        Assert.True(component.Instance._isLoading);
        Assert.Contains(
            "Loading project",
            component.Find("[role=status]").TextContent,
            StringComparison.Ordinal
        );
        Assert.Empty(component.FindAll("#projectNameHeading"));

        latestVisitResult.SetResult(ProjectResult(2, "Current project"));
        await latestVisit;

        Assert.False(component.Instance._isLoading);
        Assert.Equal("Current project", component.Find("#projectNameHeading").TextContent);

        static DataResult<ProjectView> ProjectResult(int id, string name) =>
            DataResult<ProjectView>.Succeeded([new() { Id = id, Name = name }], 1);
    }

    [Fact]
    public void ApiFailureShowsRetryInsteadOfProjectNotFound()
    {
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(DataResult<ProjectView>.Faulted("unavailable"));
        IRenderedComponent<ProjectCard> component = TestContext.Render<ProjectCard>(p =>
            p.Add(x => x.Id, 1)
        );
        component.WaitForAssertion(() =>
            Assert.Contains("Could not load this project", component.Markup)
        );
        Assert.Contains("Try again", component.Markup);
    }
}

public class ProjectValidationOrderingTests : TestBase
{
    [Fact]
    public async Task SlowValidResponseCannotEnableSaveForANewerInvalidName()
    {
        TaskCompletionSource<bool> oldResult = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        TaskCompletionSource oldRequestStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Api.GetProjectViews(Arg.Any<DataQuery>(), Arg.Any<CancellationToken>())
            .Returns(
                DataResult<ProjectView>.Succeeded(
                    [new() { Id = 1, Name = "Initial project name" }],
                    1
                )
            );
        Api.IsProjectNameAvailable("Valid older name", 1, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                oldRequestStarted.SetResult();
                return oldResult.Task;
            });
        Api.IsProjectNameAvailable("Already taken name", 1, Arg.Any<CancellationToken>())
            .Returns(false);
        IRenderedComponent<ProjectCard> component = TestContext.Render<ProjectCard>(p =>
            p.Add(x => x.Id, 1)
        );
        component.Find("#editProjectNameButton").Click();
        IRenderedComponent<ValidatedTextBox<ProjectView>> input =
            component.FindComponent<NuclearEvaluation.Client.Shared.Generics.ValidatedTextBox<ProjectView>>();
        Task older = null!;
        await component.InvokeAsync(() =>
        {
            older = input.Instance.OnInput(
                new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Valid older name" }
            );
        });
        await oldRequestStarted.Task.WaitAsync(DefaultWaitForStateTimeout);
        await input.InvokeAsync(() =>
            input.Instance.OnInput(
                new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Already taken name" }
            )
        );
        oldResult.SetResult(true);
        await older;
        Assert.True(component.Find("#saveProjectNameButton").HasAttribute("disabled"));
        Assert.Contains("Name is already in use", component.Markup);
    }
}
