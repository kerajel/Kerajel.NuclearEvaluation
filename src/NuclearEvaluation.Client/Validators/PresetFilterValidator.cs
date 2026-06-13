using FluentValidation;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Filters;

namespace NuclearEvaluation.Client.Validators;

public class PresetFilterValidator : AbstractValidator<PresetFilter>
{
    public PresetFilterValidator(INuclearEvaluationApi api)
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
                await api.IsPresetFilterNameAvailable(value, entry.Id, ct))
            .WithMessage("Name is already in use");
        });
    }
}
