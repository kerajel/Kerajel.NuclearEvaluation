using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Kernel.Models.DataManagement.PMI;

public class PmiReportDistributionEntry
{
    public int Id { get; set; }

    public int PmiReportId { get; set; }

    public PmiReport PmiReport { get; set; } = null!;

    public PmiReportDistributionChannel PmiReportDistributionChannel { get; set; }

    public PmiReportDistributionStatus PmiReportDistributionStatus { get; set; }
}
