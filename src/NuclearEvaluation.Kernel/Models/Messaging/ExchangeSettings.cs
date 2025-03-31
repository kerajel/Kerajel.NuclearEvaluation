namespace NuclearEvaluation.Kernel.Models.Messaging;

public class ExchangeSettings
{
    public required string Exchange { get; init; }
    public required string RoutingKey { get; init; }
}
