using FluentValidation;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Shared.Validators;

public class PmiReportSubmissionValidator : AbstractValidator<PmiReportSubmission>
{
    public PmiReportSubmissionValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        int nameMinLength = 10;
        int nameMaxLength = 50;

        RuleFor(x => x.ReportName)
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Length >= nameMinLength && value.Length <= nameMaxLength)
            .WithMessage($"Name must be between {nameMinLength} and {nameMaxLength} characters long");

        RuleFor(x => x.ReportDate)
            .Must(value => value.HasValue)
            .WithMessage("Date should not be empty")
            .DependentRules(() =>
            {
                RuleFor(x => x.ReportDate)
                    .Must(value => value.Value >= DateOnly.FromDateTime(DateTime.UtcNow))
                    .WithMessage("Date should not be in the past");
            });
    }
}