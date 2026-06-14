using FluentValidation;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Client.Validators;

public class ProjectViewValidator : AbstractValidator<ProjectView>
{
    public ProjectViewValidator(INuclearEvaluationApi api)
    {
        int nameMinLength = 10;
        int nameMaxLength = 50;

        RuleFor(x => x.Name).Must((name) =>
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length >= nameMinLength && name.Length <= nameMaxLength;
        }).WithMessage($"Name must be between {nameMinLength} and {nameMaxLength} characters long")
        .DependentRules(() =>
        {
            RuleFor(x => x.Name).MustAsync(async (project, name, ct) =>
                await api.IsProjectNameAvailable(name, project.Id, ct))
            .WithMessage("Name is already in use");
        });

        int conclusionsMaxLength = 400;

        RuleFor(x => x.Conclusions).Must((c) =>
        {
            return c.Length <= conclusionsMaxLength;
        }).WithMessage($"Maximum length is {conclusionsMaxLength} characters long");

        int followUpActionsRecommendedMaxLength = 400;

        RuleFor(x => x.FollowUpActionsRecommended).Must((c) =>
        {
            return c.Length <= followUpActionsRecommendedMaxLength;
        }).WithMessage($"Maximum length is {followUpActionsRecommendedMaxLength} characters long");
    }
}
