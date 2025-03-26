using Hangfire;

namespace NuclearEvaluation.HangfireJobs;

public static class JobScheduler
{
    public static void RegisterJobs()
    {
        BackgroundJob.Enqueue<DemoJob>(job => job.RunDemoTask("Hello from Enqueue!"));

        BackgroundJob.Schedule<DemoJob>(job => job.RunDemoTask("Hello from Schedule!"), TimeSpan.FromSeconds(10));

        RecurringJob.AddOrUpdate<DemoJob>("DemoRecurringJob"
            , job => job.RunDemoTask("Hello from Recurring!"), Cron.Minutely);
    }
}
