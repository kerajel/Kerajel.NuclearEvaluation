namespace NuclearEvaluation.Kernel.Interfaces;

public interface IMessager
{
    Task PublishMessageAsync<T>(string exchangeName, string routingKey, IEnumerable<T> messages, CancellationToken ct = default);
}