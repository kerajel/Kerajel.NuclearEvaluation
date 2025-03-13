using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Messaging;
using RabbitMQ.Client;
using System.Text.Json;

namespace NuclearEvaluation.Server.Services;

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

    public async Task PublishMessageAsync<T>(T message, string queueName)
    {
        using IConnection connection = await _connectionFactory.CreateConnectionAsync();
        using IChannel channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null);

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);

        //TODO Publish to an exchange, not queue
        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body: body);
    }
}