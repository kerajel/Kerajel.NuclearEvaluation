using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.PmiReportDistributionCoordinator.Models;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;

public interface IPmiReportDistributionService
{
    Task<FetchDataResult<PmiReportDistributionQueueItem>> GetQueueItems(int take, CancellationToken ct = default);
    Task<OperationResult> SetPmiReportDistributionEntryStatus(PmiReportDistributionStatus status, int entryId, CancellationToken ct = default);
}