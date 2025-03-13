using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Models.Messaging;
using NuclearEvaluation.Kernel.Models.Messaging.StemPreview;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;

namespace StemPreviewProcessor;

public class Worker : BackgroundService
{
    readonly ILogger<Worker> _logger;
    readonly ConnectionFactory _connectionFactory;

    public Worker(
        ILogger<Worker> logger,
        IOptions<RabbitMQSettings> rabbitMqSettings)
    {
        _logger = logger;

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


    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        using IConnection connection = await _connectionFactory.CreateConnectionAsync();
        using IChannel channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            byte[] body = ea.Body.ToArray();
            ProcessStemPreviewMessage? message = JsonSerializer.Deserialize<ProcessStemPreviewMessage>(ea.Body.Span);
            if (message is null)
            {
                _logger.LogError("Failed to deserialize instance of {classType}", nameof(ProcessStemPreviewMessage));
                return;
            }

            _logger.LogInformation($"Processed {message.FileId}");
            await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            return;
        };

        await channel.BasicConsumeAsync("StemPreviewProcessingQueue", autoAck: false, consumer: consumer, cancellationToken: stoppingToken);


        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}