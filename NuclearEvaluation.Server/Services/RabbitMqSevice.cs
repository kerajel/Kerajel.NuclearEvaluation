using System;
using System.Text;
using RabbitMQ.Client;
using Microsoft.Extensions.Logging;

namespace MyProject.Services
{
    public interface IRabbitMQService : IDisposable
    {
        void SendStemPreviewFileReady(Guid fileId);
    }

    // Primary constructor used to inject the logger.
    public class RabbitMQService(ILogger<RabbitMQService> logger) : IRabbitMQService
    {
        // RabbitMQ connection constants
        private const string HOST = "sparrow.rmq.cloudamqp.com";
        private const string USERNAME = "xudptmtj";
        private const string PASSWORD = "";
        private const string VIRTUAL_HOST = "xudptmtj";
        private const int PORT = 5672; // Use 5671 for TLS if required

        // Queue name for publishing StemPreviewFileReady messages.
        private const string QUEUE_STEM_PREVIEW = "StemPreviewFileReadyQueue";

        // Logger injected via primary constructor.
        private readonly ILogger<RabbitMQService> _logger = logger;

        // Publishes a message indicating that a StemPreviewFile identified by a Guid is ready.
        public void SendStemPreviewFileReady(Guid fileId)
        {
            // Configure connection factory.
            var factory = new ConnectionFactory
            {
                HostName = HOST,
                UserName = USERNAME,
                Password = PASSWORD,
                VirtualHost = VIRTUAL_HOST,
                Port = PORT
            };

            // Create connection and channel with modern using declarations.
            using var connection = factory.CreateConnectionAsync();
            using var channel = connection.Mode();

            // Declare the queue (if not already declared).
            channel.QueueDeclare
            (
                queue: QUEUE_STEM_PREVIEW,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // Prepare and publish the message.
            string message = $"StemPreviewFileReady: {fileId}";
            byte[] messageBody = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish
            (
                exchange: "",
                routingKey: QUEUE_STEM_PREVIEW,
                basicProperties: null,
                body: messageBody
            );

            _logger.LogInformation("Sent message: {Message}", message);
        }

        // No state to dispose since connection and channel are disposed per call.
        public void Dispose()
        {
        }
    }
}
