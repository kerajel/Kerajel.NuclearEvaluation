using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using Kerajel.Primitives.Models;
using NuclearEvaluation.PmiReportDistributionContracts.Messages;

namespace NuclearEvaluation.PmiReportEmailDistributor.Consumers;

internal sealed class PmiReportEmailDistributionMessageConsumer : BackgroundService
{
    readonly IConnectionFactory _connectionFactory;
    readonly IServiceScopeFactory _scopeFactory;
    readonly ILogger<PmiReportEmailDistributionMessageConsumer> _logger;
    readonly string _queueName;

    IConnection? _connection;
    IChannel? _channel;

    public PmiReportEmailDistributionMessageConsumer(
        IConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<PmiReportEmailDistributionMessageConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queueName = "PmiReportDistributionEmailQueue";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting consumer for queue {Queue}", _queueName);

        _connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(default, stoppingToken);

        AsyncEventingBasicConsumer consumer = new(_channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();

                ReadOnlyMemory<byte> body = ea.Body;
                var message = JsonSerializer.Deserialize<PmiReportDistributionMessage>(body.Span)!;

                using IDisposable? logScope = _logger.BeginScope(
                    "Received PMI Report Email Distribution message for {PmiReportId}",
                    message.PmiReportId);

                _logger.LogInformation("Received message");

                //TODO Process message

                OperationResult result = OperationResult.Succeeded();

                if (!result.IsSuccessful)
                {
                    throw new InvalidOperationException("Failed to process reply message", result.Exception!);
                }

                await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message");
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);
    }
}