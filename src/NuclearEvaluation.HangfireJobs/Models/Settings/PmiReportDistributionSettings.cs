namespace NuclearEvaluation.HangfireJobs.Models.Settings;

public class PmiReportDistributionSettings
{
    public Dictionary<string, ExchangeInfo> DistributionMap { get; set; } = [];
}