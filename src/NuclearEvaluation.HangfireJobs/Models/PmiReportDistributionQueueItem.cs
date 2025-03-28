using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.HangfireJobs.Models;

public class PmiReportDistributionQueueItem
{
    public required Guid PmiReportId { get; init; }
    public required int PmiReportDistributionEntryId { get; init; }
    public required PmiReportDistributionChannel DistributionChannel { get; init; }
}