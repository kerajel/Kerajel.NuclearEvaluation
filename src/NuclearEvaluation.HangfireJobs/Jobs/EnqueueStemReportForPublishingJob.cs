using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.HangfireJobs.Interfaces;
using NuclearEvaluation.HangfireJobs.Models;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using Z.EntityFramework.Plus;

namespace NuclearEvaluation.HangfireJobs.Jobs;

public partial class EnqueueStemReportForPublishingJob : IEnqueueStemReportForPublishingJob
{
    const int maxEntriesPerOperation = 32_767;


    readonly NuclearEvaluationServerDbContext _dbContext;

    public EnqueueStemReportForPublishingJob(NuclearEvaluationServerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Execute()
    {
        //TODO return via service
        PmiReportDistributionQueueItem[] pendingEntries = await _dbContext.PmiReportDistributionEntry
            .Where(e => e.PmiReportDistributionStatus == PmiReportDistributionStatus.Pending
                && e.PmiReport.Status == PmiReportStatus.Uploaded)
            .OrderBy(x => x.PmiReport.CreatedDate)
            .ThenBy(x => x.PmiReportId)
            .Take(maxEntriesPerOperation)
            .Select(x => new PmiReportDistributionQueueItem() 
            {
                PmiReportId = x.PmiReportId,
                PmiReportDistributionEntryId = x.Id,
                DistributionChannel = x.DistributionChannel
            })
            .ToArrayAsync();


        foreach (var entry in pendingEntries)
        {
            //entry.PmiReportDistributionStatus = PmiReportDistributionStatus.InProgress;
        }
    }
}