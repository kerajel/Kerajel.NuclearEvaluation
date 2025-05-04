using NuclearEvaluation.Abstractions.Enums;

namespace NuclearEvaluation.Kernel.Models.DataManagement.PMI;

public class PmiReportDistributionEntry
{
    public int Id { get; set; }

    public Guid PmiReportId { get; set; }

    public PmiReport PmiReport { get; set; } = null!;

    public PmiReportDistributionChannel DistributionChannel { get; set; }

    public PmiReportDistributionStatus DistributionStatus { get; set; }
}