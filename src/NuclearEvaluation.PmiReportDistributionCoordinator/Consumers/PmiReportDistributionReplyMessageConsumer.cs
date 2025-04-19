using LinqToDB;
using MassTransit;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Helpers;
using NuclearEvaluation.Kernel.Messages.PMI;
using System.Transactions;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Consumers;

public sealed class PmiReportDistributionReplyMessageConsumer : IConsumer<PmiReportDistributionReplyMessage>
{
    readonly ILogger<PmiReportDistributionReplyMessageConsumer> _logger;
    readonly NuclearEvaluationServerDbContext _dbContext;

    public PmiReportDistributionReplyMessageConsumer(
        ILogger<PmiReportDistributionReplyMessageConsumer> logger,
        NuclearEvaluationServerDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<PmiReportDistributionReplyMessage> context)
    {
        PmiReportDistributionReplyMessage message = context.Message;

        using IDisposable? scope = _logger.BeginScope(
            "PMI Report Distribution reply message for {PmiReportId}, Channel {Channel}",
            message.PmiReportId,
            message.Channel);

        _logger.LogInformation("Received message");

        using TransactionScope ts = TransactionProvider.CreateScope();

        await _dbContext.PmiReportDistributionEntry
            .Where(x => x.PmiReportId == message.PmiReportId
                     && x.DistributionChannel == message.Channel)
            .Set(x => x.DistributionStatus, PmiReportDistributionStatus.Completed)
        .UpdateAsync();

        await _dbContext.PmiReport
            .Where(r =>
                    _dbContext.PmiReportDistributionEntry.Any(e => e.PmiReportId == r.Id) &&
                    _dbContext.PmiReportDistributionEntry
                        .Where(e => e.PmiReportId == r.Id)
                        .All(e => e.DistributionStatus == PmiReportDistributionStatus.Completed))
                .Set(r => r.Status, PmiReportStatus.Distributed)
                .UpdateAsync();

        ts.Complete();

        _logger.LogInformation("Acknowledged message");
    }
}