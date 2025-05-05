using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using Kerajel.Primitives.Models;
using NuclearEvaluation.PmiReportDistributionContracts.Messages;
using NuclearEvaluation.Messaging.Interfaces;
using NuclearEvaluation.Abstractions.Enums;
using System.Text;
using NuclearEvaluation.Messaging.Parsers;
using NuclearEvaluation.PmiReportEmailDistributor.Settings;
using Microsoft.Extensions.Options;

namespace NuclearEvaluation.PmiReportEmailDistributor.Consumers;

internal sealed class PmiReportEmailDistributionMessageConsumer : BackgroundService
{
    readonly IConnectionFactory _connectionFactory;
    readonly IServiceScopeFactory _scopeFactory;
    readonly ILogger<PmiReportEmailDistributionMessageConsumer> _logger;
    readonly PmiReportDistributionSettings _settings;

    IConnection? _connection;
    IChannel? _channel;

    public PmiReportEmailDistributionMessageConsumer(
        IConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<PmiReportDistributionSettings> options,
        ILogger<PmiReportEmailDistributionMessageConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting consumer for queue {Queue}", _settings.ConsumeQueueName);

        _connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(default, stoppingToken);

        AsyncEventingBasicConsumer consumer = new(_channel);

        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

                OperationResult<PmiReportDistributionMessage> parseResult = MessageParser.TryParseMessage<PmiReportDistributionMessage>(ea.Body);

                if (!parseResult.IsSuccessful)
                {
                    _logger.LogError("Failed to deserialize {PmiReportDistributionMessage}", nameof(PmiReportDistributionMessage));
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                PmiReportDistributionMessage message = parseResult.Content!;

                using IDisposable? logScope = _logger.BeginScope(
                    "Received PMI Report Email Distribution message for {PmiReportId}",
                    message.PmiReportId);

                _logger.LogInformation("Received message");

                // Simulated processing — no actual email dispatch
                // In production: process, persist, and enqueue to outbox
                // Here: reply directly for demonstration purposes

                OperationResult result = OperationResult.Succeeded();

                if (!result.IsSuccessful)
                {
                    throw new InvalidOperationException("Failed to process reply message", result.Exception!);
                }

                //await _channel.BasicAckAsync(ea.DeliveryTag, multiple: false);

                IMessageDispatcher messageDispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();

                PmiReportDistributionReplyMessage replyMessage = new(
                    message.PmiReportId,
                    PmiReportDistributionChannel.Email,
                    PmiReportDistributionStatus.Completed);

                await messageDispatcher.Send(replyMessage, _settings.ReplyExchangeName, string.Empty, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message");
                await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: _settings.ConsumeQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);
    }
}