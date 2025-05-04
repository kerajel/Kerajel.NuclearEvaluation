using Hangfire;
using NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;

namespace NuclearEvaluation.PmiReportDistributionCoordinator;

public static class JobScheduler
{
    const string _enqueueStemReportForPublishingJobId = "521D6C88-F970-49BD-8806-5A38FF33E1BC";

    public static void RegisterJobs()
    {
        RecurringJob.AddOrUpdate<IEnqueuePmiReportForPublishingJob>(_enqueueStemReportForPublishingJobId,
            job => job.Execute(), Cron.Minutely);
    }
}
