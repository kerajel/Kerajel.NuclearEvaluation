using Kerajel.Primitives.Models;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;

public interface IPmiReportDistributionMessageDispatcher
{
    Task<OperationResult> Send<T>(T message, string exchange, string routingKey, CancellationToken ct = default);
}