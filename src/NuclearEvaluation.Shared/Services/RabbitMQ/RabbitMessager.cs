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

    public async Task PublishMessageAsync<T>(string exchangeName, string routingKey, params T[] messages)
    {
        await PublishMessageAsync(exchangeName, routingKey, messages);
    }

    public async Task PublishMessageAsync<T>(string exchangeName, string routingKey, IEnumerable<T> messages)
    {
        using IConnection connection = await _connectionFactory.GetConnection();
        using IChannel channel = await connection.CreateChannelAsync();

        foreach (T? message in messages)
        {
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);
            await channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, body: body);
        }
    }
}