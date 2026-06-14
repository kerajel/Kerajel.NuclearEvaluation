using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using NuclearEvaluation.Client.Pages;
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

    IRenderedComponent<ProjectCard> RenderProjectCard(ProjectView project)
        => TestContext.Render<ProjectCard>(parameters => parameters
            .Add(p => p.Id, project.Id)
            .Add(p => p.TabRenderMode, Radzen.TabRenderMode.Server));

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
    public void RenameProject_WhenValidInput_ShouldUpdateProjectName()
    {
        ProjectView project = Project(1, "Initial Project Name");
        const string newProjectName = "Updated Project Name";
        SetupProject(project);
        Api.IsProjectNameAvailable(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);

        IRenderedComponent<ProjectCard> component = RenderProjectCard(project);

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#editProjectNameButton").Click();
        component.WaitForState(() => component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        component.Find("#projectNameInput").Input(newProjectName);
        component.Find("#saveProjectNameButton").Click();

        component.WaitForState(() => !component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        component.Find("#projectNameHeading").TextContent.ShouldBe(newProjectName);
        Api.Received().UpdateProjectField(
            Arg.Is<ProjectFieldUpdate>(u => u.Field == ProjectField.Name && u.StringValue == newProjectName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RenameProject_WhenNameAlreadyExists_ShouldInvalidateInput()
    {
        ProjectView project = Project(1, "ProjectNameA");
        SetupProject(project);
        Api.IsProjectNameAvailable(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(false);

        IRenderedComponent<ProjectCard> component = RenderProjectCard(project);

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#editProjectNameButton").Click();
        component.WaitForState(() => component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        component.Find("#projectNameInput").Input("ProjectNameB");

        component.WaitForAssertion(() =>
        {
            IElement saveButton = component.Find("#saveProjectNameButton");
            saveButton.IsDisabled().ShouldBeTrue();

            IElement validationLabel = component.Find(".validation-tooltip");
            validationLabel.TextContent.ShouldBe("Name is already in use");
        }, timeout: TimeSpan.FromSeconds(10));
    }
}
