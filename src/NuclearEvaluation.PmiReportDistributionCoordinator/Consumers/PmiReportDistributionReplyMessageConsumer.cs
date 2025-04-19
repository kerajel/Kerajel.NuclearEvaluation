using MassTransit;
using NuclearEvaluation.Kernel.Messages.PMI;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Consumers;

public sealed class PmiReportDistributionReplyMessageConsumer : IConsumer<PmiReportDistributionReplyMessage>
{
    private readonly ILogger<PmiReportDistributionReplyMessageConsumer> _logger;

    public PmiReportDistributionReplyMessageConsumer(ILogger<PmiReportDistributionReplyMessageConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<PmiReportDistributionReplyMessage> context)
    {
        _logger.LogInformation("Received reply message: {@ReplyMessage}", context.Message);
        return Task.CompletedTask;
    }
}
