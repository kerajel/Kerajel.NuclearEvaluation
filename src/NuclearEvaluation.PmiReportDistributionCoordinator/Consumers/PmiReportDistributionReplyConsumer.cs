using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using NuclearEvaluation.PmiReportDistributionCoordinator.Models.Settings;
using System.Text.Json;
using NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;
using Kerajel.Primitives.Models;
using NuclearEvaluation.PmiReportDistributionContracts.Messages;
using NuclearEvaluation.Messaging.Parsers;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Consumers;

internal sealed class PmiReportDistributionReplyConsumer : BackgroundService
{
    readonly IConnectionFactory _connectionFactory;
    readonly IServiceScopeFactory _scopeFactory;
    readonly ILogger<PmiReportDistributionReplyConsumer> _logger;
    readonly string _queueName;

    IConnection? _connection;
    IChannel? _channel;

    public PmiReportDistributionReplyConsumer(
        IConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<PmiReportDistributionSettings> options,
        ILogger<PmiReportDistributionReplyConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queueName = options.Value.ReplyQueueName;
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
                IPmiReportDistributionService pmiService = scope.ServiceProvider.GetRequiredService<IPmiReportDistributionService>();

                OperationResult<PmiReportDistributionReplyMessage> parseResult = MessageParser.TryParseMessage<PmiReportDistributionReplyMessage>(ea.Body);

                if (!parseResult.IsSuccessful)
                {
                    _logger.LogError("Failed to deserialize {PmiReportDistributionReplyMessage}", nameof(PmiReportDistributionReplyMessage));
                    await _channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                    return;
                }

                PmiReportDistributionReplyMessage? message = parseResult.Content!;

                using IDisposable? logScope = _logger.BeginScope(
                    "PMI Report Distribution reply message for {PmiReportId}, Channel {Channel}",
                    message.PmiReportId,
                    message.Channel);

                _logger.LogInformation("Received message");

                OperationResult result = await pmiService.ProcessReplyMessage(message, stoppingToken);

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