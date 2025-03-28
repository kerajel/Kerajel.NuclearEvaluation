using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.HangfireJobs.Interfaces;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using Polly;
using Z.EntityFramework.Plus;

namespace NuclearEvaluation.HangfireJobs.Jobs;

public class EnqueueStemReportForPublishingJob : IEnqueueStemReportForPublishingJob
{
    const int maxEntriesPerOperation = 32_767;


    readonly NuclearEvaluationServerDbContext _dbContext;

    public EnqueueStemReportForPublishingJob(NuclearEvaluationServerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Execute()
    {
        PmiReportDistributionEntry[] pendingEntries = await _dbContext.PmiReportDistributionEntry
            .IncludeOptimized(e => e.PmiReport)
            .Where(e => e.PmiReportDistributionStatus == PmiReportDistributionStatus.Pending
                && e.PmiReport.Status == PmiReportStatus.Uploaded)
            .OrderBy(x => x.PmiReport.CreatedDate)
            .ThenBy(x => x.PmiReportId)
            .Take(maxEntriesPerOperation)
            .ToArrayAsync();
    }
}
