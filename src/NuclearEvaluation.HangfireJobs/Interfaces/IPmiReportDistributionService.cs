using Kerajel.Primitives.Models;
using NuclearEvaluation.HangfireJobs.Models;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.HangfireJobs.Interfaces;

public interface IPmiReportDistributionService
{
    Task<FetchDataResult<PmiReportDistributionQueueItem>> GetQueueItems(int take, CancellationToken ct = default);
    Task<OperationResult> SetPmiReportDistributionEntryStatus(PmiReportDistributionStatus status, IEnumerable<int> entryIds, CancellationToken ct = default);
}