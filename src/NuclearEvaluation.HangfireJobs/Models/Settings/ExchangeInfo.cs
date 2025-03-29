namespace NuclearEvaluation.HangfireJobs.Models.Settings;

public class ExchangeInfo
{
    public required string Exchange { get; init; }
    public required string RoutingKey { get; init; }
}