using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Kernel.Messages.PMI;

public record PmiReportDistributionReplyMessage(
    Guid PmiReportId,
    PmiReportDistributionChannel Channel,
    PmiReportDistributionStatus Status);