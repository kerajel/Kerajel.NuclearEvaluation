using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Shared.Generics;
using NuclearEvaluation.Client.Validators;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Client.Shared.Evaluation.Project;

public partial class ProjectOverview : ComponentBase
{
    [Inject]
    public ProjectViewValidator ProjectViewValidator { get; set; } = null!;

    [Inject]
    public INuclearEvaluationApi Api { get; set; } = null!;

    [Parameter]
    public ProjectView ProjectView { get; set; } = null!;

    ValidatedTextArea<ProjectView> _projectConclusionsInputRef = null!;
    ValidatedTextArea<ProjectView> _projectFollowUpActionsRecommendedInputRef = null!;

    async Task OnConclusionsBlur()
    {
        if (_projectConclusionsInputRef.IsReadyToCommit())
        {
            await Api.UpdateProjectField(new ProjectFieldUpdate
            {
                ProjectId = ProjectView.Id,
                Field = ProjectField.Conclusions,
                StringValue = ProjectView.Conclusions,
            });

            _projectConclusionsInputRef.Commit();
        }
    }

    async Task OnFollowUpActionsRecommendedBlur()
    {
        if (_projectFollowUpActionsRecommendedInputRef.IsReadyToCommit())
        {
            await Api.UpdateProjectField(new ProjectFieldUpdate
            {
                ProjectId = ProjectView.Id,
                Field = ProjectField.FollowUpActionsRecommended,
                StringValue = ProjectView.FollowUpActionsRecommended,
            });

            _projectFollowUpActionsRecommendedInputRef.Commit();
        }
    }
}
