using NuclearEvaluation.Abstractions.Enums;

namespace NuclearEvaluation.PmiReportDistributionContracts.Messages;

public record PmiReportDistributionReplyMessage(
    Guid PmiReportId,
    PmiReportDistributionChannel Channel,
    PmiReportDistributionStatus Status);