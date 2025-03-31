using RabbitMQ.Client;

namespace NuclearEvaluation.Kernel.Interfaces.RabbitMQ;

public interface IAmqpConnectionFactory : IDisposable
{
    Task<IConnection> GetConnection(CancellationToken ct = default);
}