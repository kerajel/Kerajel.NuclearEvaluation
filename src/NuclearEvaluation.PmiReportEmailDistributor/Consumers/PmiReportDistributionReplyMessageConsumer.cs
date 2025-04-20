using MassTransit;
using NuclearEvaluation.Kernel.Messages.PMI;

namespace NuclearEvaluation.PmiReportEmailDistributor.Consumers;

public sealed class PmiReportDistributionReplyMessageConsumer : IConsumer<PmiReportDistributionMessage>
{
    readonly ILogger<PmiReportDistributionReplyMessageConsumer> _logger;

    public PmiReportDistributionReplyMessageConsumer(
        ILogger<PmiReportDistributionReplyMessageConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PmiReportDistributionMessage> context)
    {
        PmiReportDistributionMessage message = context.Message;

        using IDisposable? scope = _logger.BeginScope(
            "PMI Report Email Distribution message for {PmiReportId}",
            message.PmiReportId);

        //Placeholder for the actual email distribution implementation
        await Task.Delay(TimeSpan.FromMinutes(1));

        _logger.LogInformation("Received message");

        _logger.LogInformation("Acknowledged message");
    }
}