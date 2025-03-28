using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Interfaces.RabbitMQ;
using NuclearEvaluation.Kernel.Models.Messaging;
using RabbitMQ.Client;

namespace NuclearEvaluation.Shared.Services.RabbitMQ;

public class ResilientConnectionFactory : IAmqpConnectionFactory
{
    readonly ConnectionFactory _connectionFactory;
    readonly SemaphoreSlim _semaphore = new(1, 1);
    readonly ILogger<ResilientConnectionFactory> _logger;

    IConnection? _connection;

    public ResilientConnectionFactory(
        IOptions<RabbitMQSettings> rabbitMqSettings,
        ILogger<ResilientConnectionFactory> logger)
    {
        RabbitMQSettings settings = rabbitMqSettings.Value;
        _connectionFactory = new ConnectionFactory
        {
            HostName = settings.HostName,
            UserName = settings.UserName,
            Password = settings.Password,
            Port = settings.Port,
            VirtualHost = settings.VirtualHost,
        };
        _logger = logger;
    }

    public async Task<IConnection> GetConnection()
    {
        if (ConnectionIsBroken())
        {
            await EnsureConnection();
        }
        return _connection!;
    }

    private async Task EnsureConnection()
    {
        await _semaphore.WaitAsync();
        try
        {
            if (ConnectionIsBroken())
            {
                if (_connection != null)
                {
                    try
                    {
                        _connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error trying to dispose {connection}", nameof(_connection));
                    }
                }

                _connection = await _connectionFactory.CreateConnectionAsync();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private bool ConnectionIsBroken() => _connection == null || !_connection.IsOpen;

    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }
}