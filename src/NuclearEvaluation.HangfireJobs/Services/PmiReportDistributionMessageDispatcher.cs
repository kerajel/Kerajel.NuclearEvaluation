using NuclearEvaluation.HangfireJobs.Models;
using NuclearEvaluation.HangfireJobs.Models.Settings;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;

namespace NuclearEvaluation.HangfireJobs.Services;

public class PmiReportDistributionMessageDispatcher
{
    readonly IMessager _messager;
    readonly ILogger<PmiReportDistributionMessageDispatcher> _logger;
    readonly PmiReportDistributionSettings _distributionSettings;

    public PmiReportDistributionMessageDispatcher(
        IMessager messager,
        ILogger<PmiReportDistributionMessageDispatcher> logger,
        PmiReportDistributionSettings distributionSettings)
    {
        _messager = messager;
        _logger = logger;
        _distributionSettings = distributionSettings;
    }
    public bool CanHandle(PmiReportDistributionChannel channel) => channel == PmiReportDistributionChannel.Email;

    public async Task Send(IEnumerable<PmiReportDistributionQueueItem> queueItems, CancellationToken? ct = default)
    {
        var groupedItems = queueItems.GroupBy(x => x.DistributionChannel);

        foreach (IGrouping<PmiReportDistributionChannel, PmiReportDistributionQueueItem> group in groupedItems)
        {
            string channelTypeName = Enum.GetName(group.Key)!;
            _ = _distributionSettings.DistributionMap.TryGetValue(channelTypeName, out ExchangeInfo? exchangeInfo);

            if (exchangeInfo == null)
            {
                _logger.LogError("Exchange mapping undefined for '{channelType}'", channelTypeName);
                continue;
            }

            string exchange = exchangeInfo.Exchange;
            string routingKey = exchangeInfo.RoutingKey;

            await _messager.PublishMessageAsync(exchange, routingKey, group);
        }
    }
}