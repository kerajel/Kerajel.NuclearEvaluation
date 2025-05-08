using Kerajel.Primitives.Models;

namespace NuclearEvaluation.Messaging.Interfaces;

public interface IMessageDispatcher
{
    Task<OperationResult> Send<T>(T message, string exchange, string routingKey, CancellationToken ct = default);
}