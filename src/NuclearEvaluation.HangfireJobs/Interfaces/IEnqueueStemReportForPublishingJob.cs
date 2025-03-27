namespace NuclearEvaluation.HangfireJobs.Interfaces;

public interface IEnqueueStemReportForPublishingJob
{
    Task Execute();
}