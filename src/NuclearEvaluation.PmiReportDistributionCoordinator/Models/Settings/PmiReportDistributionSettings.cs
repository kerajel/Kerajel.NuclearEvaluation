namespace NuclearEvaluation.PmiReportDistributionCoordinator.Models.Settings;

public class PmiReportDistributionSettings
{
    public required Dictionary<string, ExchangeInfo> DistributionMap { get; init; }
    public required string ReplyQueueName { get; init; }
}