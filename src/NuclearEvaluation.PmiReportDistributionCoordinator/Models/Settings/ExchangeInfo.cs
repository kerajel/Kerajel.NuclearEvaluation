namespace NuclearEvaluation.PmiReportDistributionCoordinator.Models.Settings;

public class ExchangeInfo
{
    public required string Exchange { get; init; }
    public string RoutingKey { get; init; } = string.Empty;
}