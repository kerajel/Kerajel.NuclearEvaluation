using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Kerajel.Primitives.Models;
using NuclearEvaluation.Messaging.Interfaces;

namespace NuclearEvaluation.Messaging.Dispatchers;

public class NuclearEvaluationMessageDispatcher : IMessageDispatcher, IAsyncDisposable
{
    readonly IConnectionFactory _connectionFactory;
    readonly SemaphoreSlim _initSemaphore = new(1, 1);

    IConnection? _connection;
    IChannel? _channel;

    public NuclearEvaluationMessageDispatcher(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OperationResult> Send<T>(
        T message,
        string exchange,
        string routingKey,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureInitializedAsync(ct);

            byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

            await _channel!.BasicPublishAsync(
                exchange: exchange,
                routingKey: routingKey,
                body: body,
                cancellationToken: ct);

            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            return OperationResult.Faulted(ex);
        }
    }

    async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            return;

        await _initSemaphore.WaitAsync(ct);
        try
        {
            if (!(_connection is { IsOpen: true }) && !(_channel is { IsOpen: true }))
            {
                CreateChannelOptions channelOptions = new(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true,
                    outstandingPublisherConfirmationsRateLimiter: null,
                    consumerDispatchConcurrency: null);

                _connection = await _connectionFactory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(channelOptions, ct);
            }
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
            await _channel.DisposeAsync();
        _connection?.Dispose();
        _initSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}
