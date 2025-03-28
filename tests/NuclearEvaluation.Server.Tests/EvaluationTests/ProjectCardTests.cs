using AngleSharp.Dom;
using Bunit;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Server.Pages;
using NuclearEvaluation.Server.Tests;
using Shouldly;

namespace NuclearEvaluation.Server.Tests.EvaluationTests;

public class EvaluationTests : TestBase
{
    [Fact]
    public async Task Render_ShouldRenderProjectName()
    {
        // Arrange
        Project project = new()
        {
            Name = "Plutonium assessment test",
        };

        await DbContext.Project.SingleInsertAsync(project);

        // Act
        IRenderedComponent<ProjectCard> component = TestContext
            .RenderComponent<ProjectCard>(parameters => parameters.Add(p => p.Id, project.Id));

        component.WaitForState(() => component.Instance._isLoading == false, DefaultWaitForStateTimeout);

        // Assert
        string renderedProjectName = component.Find("#projectNameHeading").TextContent;
        renderedProjectName.ShouldBe(project.Name);
    }

    [Fact]
    public async Task RenameProject_WhenValidInput_ShouldUpdateProjectName()
    {
        // Arrange
        Project project = new()
        {
            Name = "Initial Project Name",
        };

        string newProjectName = "Updated Project Name";

        await DbContext.Project.SingleInsertAsync(project);

        // Act
        IRenderedComponent<ProjectCard> component = TestContext
            .RenderComponent<ProjectCard>(parameters => parameters.Add(p => p.Id, project.Id));

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#editProjectNameButton").Click();

        component.WaitForState(() => component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        component.Find("#projectNameInput").Input(newProjectName);

        component.Find("#saveProjectNameButton").Click();

        component.WaitForState(() => !component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        // Assert
        string renderedProjectName = component.Find("#projectNameHeading").TextContent;
        renderedProjectName.ShouldBe(newProjectName);

        await DbContext.Entry(project).ReloadAsync();
        project.Name.ShouldBe(newProjectName);
    }

    [Fact]
    public async Task RenameProject_WhenNameAlreadyExists_ShouldInvalidateInput()
    {
        // Arrange
        Project projectA = new()
        {
            Name = "ProjectNameA",
        };

        Project projectB = new()
        {
            Name = "ProjectNameB",
        };

        DbContext.AddRange(projectA, projectB);

        await DbContext.SaveChangesAsync();

        // Act
        IRenderedComponent<ProjectCard> component = TestContext
            .RenderComponent<ProjectCard>(parameters => parameters.Add(p => p.Id, projectA.Id));

        component.WaitForState(() => !component.Instance._isLoading, DefaultWaitForStateTimeout);

        component.Find("#editProjectNameButton").Click();

        component.WaitForState(() => component.Instance._isEditingProjectName, DefaultWaitForStateTimeout);

        component.Find("#projectNameInput").Input(projectB.Name);

        // Assert
        component.WaitForAssertion(async () =>
        {
            await Task.Yield();
            IElement saveButton = component.Find("#saveProjectNameButton");
            saveButton.IsDisabled().ShouldBeTrue();

            IElement validationLabel = component.Find("#validationTooltip");
            validationLabel.TextContent.ShouldBe("Name is already in use");

        }, timeout: TimeSpan.FromSeconds(10));
    }
}