using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Server.Shared.Generics;
using NuclearEvaluation.Server.Validators;

namespace NuclearEvaluation.Server.Shared.Evaluation.Project;

public partial class ProjectOverview : ComponentBase
{
    [Inject]
    public ProjectViewValidator ProjectViewValidator { get; set; } = null!;

    [Inject]
    public IProjectService ProjectService { get; set; } = null!;

    [Parameter]
    public ProjectView ProjectView { get; set; } = null!;

    ValidatedTextArea<ProjectView> _projectConclusionsInputRef = null!;
    ValidatedTextArea<ProjectView> _projectFollowUpActionsRecommendedInputRef = null!;

    async Task OnConclusionsBlur()
    {
        if (await _projectConclusionsInputRef.IsReadyToCommit())
        {
            await ProjectService.UpdatePropertyFromView(
                ProjectView,
                x => x.Conclusions,
                x => x.Conclusions);

            _projectConclusionsInputRef.Commit();
        }
    }

    async Task OnFollowUpActionsRecommendedBlur()
    {
        if (await _projectFollowUpActionsRecommendedInputRef.IsReadyToCommit())
        {
            await ProjectService.UpdatePropertyFromView(
                ProjectView,
                x => x.FollowUpActionsRecommended,
                x => x.FollowUpActionsRecommended);

            _projectFollowUpActionsRecommendedInputRef.Commit();
        }
    }
}