﻿using Hangfire;
using NuclearEvaluation.HangfireJobs.Interfaces;

namespace NuclearEvaluation.HangfireJobs;

public static class JobScheduler
{
    const string enqueueStemReportForPublishingJobId = "521D6C88-F970-49BD-8806-5A38FF33E1BC";

    public static void RegisterJobs()
    {
        RecurringJob.AddOrUpdate<IEnqueueStemReportForPublishingJob>(enqueueStemReportForPublishingJobId,
            job => job.Execute(), Cron.Minutely);
    }
}
