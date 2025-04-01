using AngleSharp.Dom;
using Bunit;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Server.Pages;
using Shouldly;

namespace NuclearEvaluation.Server.Tests.EvaluationTests;

public class ProjectCardTests : TestBase
{
    public ProjectCardTests(TestFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Render_ShouldRenderProjectName()
    {
        Project project = new Project
        {
            Name = "Plutonium assessment test"
        };

        await DbContext.Project.SingleInsertAsync(project);

        IRenderedComponent<ProjectCard> component = TestContext
            .RenderComponent<ProjectCard>(parameters => parameters.Add(p => p.Id, project.Id));

        component.WaitForState(() => component.Instance._isLoading == false, DefaultWaitForStateTimeout);

        string renderedProjectName = component.Find("#projectNameHeading").TextContent;
        renderedProjectName.ShouldBe(project.Name);
    }

    [Fact]
    public async Task RenameProject_WhenValidInput_ShouldUpdateProjectName()
    {
        Project project = new Project
        {
            Name = "Initial Project Name"
        };

        string newProjectName = "Updated Project Name";

        await DbContext.Project.SingleInsertAsync(project);

        IRenderedComponent<ProjectCard> component = TestContext
            .RenderComponent<ProjectCard>(parameters => parameters.Add(p => p.Id, project.Id));

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#editProjectNameButton").Click();

        component.WaitForState(() => component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        component.Find("#projectNameInput").Input(newProjectName);

        component.Find("#saveProjectNameButton").Click();

        component.WaitForState(() => !component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        string renderedProjectName = component.Find("#projectNameHeading").TextContent;
        renderedProjectName.ShouldBe(newProjectName);

        await DbContext.Entry(project).ReloadAsync();
        project.Name.ShouldBe(newProjectName);
    }

    [Fact]
    public async Task RenameProject_WhenNameAlreadyExists_ShouldInvalidateInput()
    {
        Project projectA = new Project
        {
            Name = "ProjectNameA"
        };

        Project projectB = new Project
        {
            Name = "ProjectNameB"
        };

        DbContext.AddRange(projectA, projectB);

        await DbContext.SaveChangesAsync();

        IRenderedComponent<ProjectCard> component = TestContext
            .RenderComponent<ProjectCard>(parameters => parameters.Add(p => p.Id, projectA.Id));

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#editProjectNameButton").Click();

        component.WaitForState(() => component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        component.Find("#projectNameInput").Input(projectB.Name);

        component.WaitForAssertion(async () =>
        {
            await Task.Yield();
            IElement saveButton = component.Find("#saveProjectNameButton");
            saveButton.IsDisabled().ShouldBeTrue();

            IElement validationLabel = component.Find("#validationTooltip");
            validationLabel.TextContent.ShouldBe("Name is already in use");
        }, timeout: System.TimeSpan.FromSeconds(10));
    }
}