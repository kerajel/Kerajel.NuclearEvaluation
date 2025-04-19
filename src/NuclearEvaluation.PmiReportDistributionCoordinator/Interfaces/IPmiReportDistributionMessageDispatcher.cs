using NuclearEvaluation.PmiReportDistributionCoordinator.Models;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;

public interface IPmiReportDistributionMessageDispatcher
{
    Task Send(IEnumerable<PmiReportDistributionQueueItem> queueItems, CancellationToken ct = default);
}