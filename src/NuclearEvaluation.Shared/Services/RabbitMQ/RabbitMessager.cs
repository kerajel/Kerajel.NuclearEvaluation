using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Interfaces.RabbitMQ;
using RabbitMQ.Client;
using System.Text.Json;

namespace NuclearEvaluation.Shared.Services.RabbitMQ;

public class RabbitMessager : IMessager
{
    private readonly IAmqpConnectionFactory _connectionFactory;

    public RabbitMessager(IAmqpConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task PublishMessageAsync<T>(string exchangeName, string routingKey, IEnumerable<T> messages, CancellationToken ct = default)
    {
        IConnection connection = await _connectionFactory.GetConnection(ct);
        using IChannel channel = await connection.CreateChannelAsync(cancellationToken: ct);

        foreach (T? message in messages)
        {
            if (ct is { IsCancellationRequested: true })
            {
                break;
            }
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);
            await channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, body: body, cancellationToken: ct);
        }
    }
}