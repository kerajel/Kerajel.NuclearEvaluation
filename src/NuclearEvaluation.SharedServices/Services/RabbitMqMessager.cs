using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Messaging;
using RabbitMQ.Client;
using System.Text.Json;

namespace NuclearEvaluation.Shared.Services;

public class RabbitMQPublisher : IMessager
{
    private readonly ConnectionFactory _connectionFactory;

    public RabbitMQPublisher(IOptions<RabbitMQSettings> rabbitMqSettings)
    {
        RabbitMQSettings settings = rabbitMqSettings.Value;
        _connectionFactory = new()
        {
            HostName = settings.HostName,
            UserName = settings.UserName,
            Password = settings.Password,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
        };
    }

    public async Task PublishMessageAsync<T>(T message, string exchangeName, string routingKey)
    {
        using IConnection connection = await _connectionFactory.CreateConnectionAsync();
        using IChannel channel = await connection.CreateChannelAsync();

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);

        await channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, body: body);
    }
}