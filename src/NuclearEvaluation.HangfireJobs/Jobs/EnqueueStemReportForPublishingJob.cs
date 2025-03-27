using NuclearEvaluation.HangfireJobs.Interfaces;

namespace NuclearEvaluation.HangfireJobs.Jobs;

public class EnqueueStemReportForPublishingJob : IEnqueueStemReportForPublishingJob
{
    public async Task Execute()
    {
        Console.WriteLine($"Running DemoJob with message: {"hi"}");
        Thread.Sleep(1000);
        Console.WriteLine("DemoJob done.");
        await Task.Yield();
    }
}
