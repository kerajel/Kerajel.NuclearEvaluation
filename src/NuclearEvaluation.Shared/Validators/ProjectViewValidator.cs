using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Data.Context;

namespace NuclearEvaluation.Shared.Validators;

public class ProjectViewValidator : AbstractValidator<ProjectView>
{
    public ProjectViewValidator(IDbContextFactory<NuclearEvaluationServerDbContext> dbContextFactory)
    {
        int nameMinLength = 10;
        int nameMaxLength = 50;

        RuleFor(x => x.Name).Must((name) =>
        {
            return !string.IsNullOrWhiteSpace(name) && name.Length >= nameMinLength && name.Length <= nameMaxLength;
        }).WithMessage($"Name must be between {nameMinLength} and {nameMaxLength} characters long")
        .DependentRules(() =>
        {
            RuleFor(x => x.Name).MustAsync(async (filter, name, ct) =>
            {
                using NuclearEvaluationServerDbContext dbContext = dbContextFactory.CreateDbContext();
                bool exists = await dbContext.Project.AnyAsync(d => d.Name == filter.Name && d.Id != filter.Id, ct);
                return !exists;
            }).WithMessage("Name is already in use");
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