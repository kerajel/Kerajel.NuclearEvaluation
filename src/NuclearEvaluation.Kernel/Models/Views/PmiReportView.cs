using NuclearEvaluation.Kernel.Enums;
using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Kernel.Models.Views;

public class PmiReportView
{
    [Key]
    public required Guid Id { get; init; }

    public required string ReportName { get; init; }

    public required DateOnly DateUploaded { get; init; }

    public required string UserName { get; init; }

    public required PmiReportStatus ReportStatus { get; init; }

    public ICollection<PmiReportDistributionEntryView> DistributionEntries { get; init; } = [];
}
