using Kerajel.Primitives.Models;
using LinqToDB;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.HangfireJobs.Models;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using LinqToDB.EntityFrameworkCore;
using NuclearEvaluation.HangfireJobs.Interfaces;

namespace NuclearEvaluation.HangfireJobs.Services;

public class PmiReportDistributionService : IPmiReportDistributionService
{
    readonly NuclearEvaluationServerDbContext _dbContext;

    public PmiReportDistributionService(NuclearEvaluationServerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FetchDataResult<PmiReportDistributionQueueItem>> GetQueueItems(int take, CancellationToken ct = default)
    {
        try
        {
            PmiReportDistributionQueueItem[] result = await _dbContext.PmiReportDistributionEntry
                .Where(e => e.PmiReportDistributionStatus == PmiReportDistributionStatus.Pending
                    && e.PmiReport.Status == PmiReportStatus.Uploaded)
                .OrderBy(x => x.PmiReport.CreatedDate)
                .ThenBy(x => x.PmiReportId)
                .Take(take)
                .Select(x => new PmiReportDistributionQueueItem()
                {
                    PmiReportId = x.PmiReportId,
                    PmiReportDistributionEntryId = x.Id,
                    DistributionChannel = x.DistributionChannel
                })
                .ToArrayAsyncLinqToDB(ct);
            return FetchDataResult<PmiReportDistributionQueueItem>.Succeeded(result);
        }
        catch (Exception ex)
        {
            return FetchDataResult<PmiReportDistributionQueueItem>.Faulted(ex);
        }
    }

    public async Task<OperationResult> SetPmiReportDistributionEntryStatus(
        PmiReportDistributionStatus status,
        IEnumerable<int> entryIds,
        CancellationToken ct = default)
    {
        try
        {
            await _dbContext.PmiReportDistributionEntry.Where(x => entryIds.Contains(x.Id))
                .Set(x => x.PmiReportDistributionStatus, status)
                .UpdateAsync(ct);

            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            return OperationResult.Faulted(ex);
        }
    }
}
