namespace NuclearEvaluation.PmiReportEmailDistributor.Settings;

public class PmiReportDistributionSettings
{
    public required string ConsumeQueueName { get; init; }
    public required string ReplyExchangeName { get; init; }
}
