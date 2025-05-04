using Kerajel.Primitives.Models;
using LinqToDB;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using LinqToDB.EntityFrameworkCore;
using NuclearEvaluation.PmiReportDistributionCoordinator.Models;
using NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Services;

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
                .Where(e => e.DistributionStatus == PmiReportDistributionStatus.Pending
                    && e.PmiReport.Status == PmiReportStatus.Uploaded)
                .OrderBy(x => x.PmiReport.CreatedDate)
                .ThenBy(x => x.PmiReportId)
                .Take(take)
                .Select(x => new PmiReportDistributionQueueItem()
                {
                    PmiReportId = x.PmiReportId,
                    PmiReportDistributionEntryId = x.Id,
                    DistributionChannel = x.DistributionChannel,
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
        int entryId,
        CancellationToken ct = default)
    {
        try
        {
            await _dbContext.PmiReportDistributionEntry.Where(x => entryId == x.Id)
                .Set(x => x.DistributionStatus, status)
                .UpdateAsync(ct);

            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            return OperationResult.Faulted(ex);
        }
    }
}