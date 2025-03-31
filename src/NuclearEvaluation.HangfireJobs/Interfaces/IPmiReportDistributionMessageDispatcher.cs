using NuclearEvaluation.HangfireJobs.Models;

namespace NuclearEvaluation.HangfireJobs.Interfaces;

public interface IPmiReportDistributionMessageDispatcher
{
    Task Send(IEnumerable<PmiReportDistributionQueueItem> queueItems, CancellationToken cancellationToken = default);
}