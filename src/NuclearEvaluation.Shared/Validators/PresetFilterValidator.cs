using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Models.Filters;
using NuclearEvaluation.Kernel.Data.Context;

namespace NuclearEvaluation.Shared.Validators;

public class PresetFilterValidator : AbstractValidator<PresetFilter>
{
    public PresetFilterValidator(IDbContextFactory<NuclearEvaluationServerDbContext> dbContextFactory)
    {
        int minLength = 5;
        int maxLength = 25;

        RuleFor(x => x.Name).Must((value) =>
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length >= minLength && value.Length <= maxLength;
        })
        .WithMessage($"Name must be between {minLength} and {maxLength} characters long")
        .DependentRules(() =>
        {
            RuleFor(x => x.Name).MustAsync(async (entry, value, ct) =>
            {
                using NuclearEvaluationServerDbContext dbContext = dbContextFactory.CreateDbContext();
                bool exists = await dbContext.PresetFilter.AnyAsync(d => d.Name == entry.Name && d.Id != entry.Id, ct);
                return !exists;
            }).WithMessage("Name is already in use");

        });
    }
}