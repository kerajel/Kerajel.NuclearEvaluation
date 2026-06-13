using FluentValidation;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Validators;

public class PmiReportSubmissionValidator : AbstractValidator<PmiReportSubmission>
{
    public PmiReportSubmissionValidator(INuclearEvaluationApi api)
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        int nameMinLength = 10;
        int nameMaxLength = 50;

        RuleFor(x => x.ReportName)
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Length >= nameMinLength && value.Length <= nameMaxLength)
            .WithMessage($"Name must be between {nameMinLength} and {nameMaxLength} characters long")
            .DependentRules(() =>
            {
                RuleFor(x => x.ReportName).MustAsync(async (report, value, ct) =>
                    await api.IsPmiReportNameAvailable(value, ct))
                .WithMessage("Name is already in use");
            });

        RuleFor(x => x.ReportDate)
            .Must(value => value.HasValue)
            .WithMessage("Date should not be empty")
            .DependentRules(() =>
            {
                RuleFor(x => x.ReportDate)
                    .Must(value => value!.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
                    .WithMessage("Date should not be this far in the future");
            });
    }
}
