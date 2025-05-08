using NuclearEvaluation.Abstractions.Enums;

namespace NuclearEvaluation.Kernel.Models.Views;

public class PmiReportDistributionEntryView
{
    public int Id { get; set; }

    public Guid PmiReportId { get; set; }

    public PmiReportView PmiReport { get; set; } = null!;

    public PmiReportDistributionChannel DistributionChannel { get; set; }

    public PmiReportDistributionStatus DistributionStatus { get; set; }
}