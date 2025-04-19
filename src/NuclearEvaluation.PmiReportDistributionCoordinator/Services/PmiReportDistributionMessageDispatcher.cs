using MassTransit;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Messages.PMI;
using NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;
using NuclearEvaluation.PmiReportDistributionCoordinator.Models;
using NuclearEvaluation.PmiReportDistributionCoordinator.Models.Settings;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Services;

public class PmiReportDistributionMessageDispatcher : IPmiReportDistributionMessageDispatcher
{
    readonly IBus _bus;
    readonly ILogger<PmiReportDistributionMessageDispatcher> _logger;
    readonly PmiReportDistributionSettings _distributionSettings;

    public PmiReportDistributionMessageDispatcher(
        IBus bus,
        ILogger<PmiReportDistributionMessageDispatcher> logger,
        IOptions<PmiReportDistributionSettings> distributionSettings)
    {
        _bus = bus;
        _logger = logger;
        _distributionSettings = distributionSettings.Value;
    }

    public async Task Send(IEnumerable<PmiReportDistributionQueueItem> queueItems, CancellationToken ct = default)
    {
        foreach (var group in queueItems.GroupBy(item => item.DistributionChannel))
        {
            string channelTypeName = Enum.GetName(group.Key) ?? string.Empty;

            _ = _distributionSettings.DistributionMap.TryGetValue(channelTypeName, out ExchangeInfo? exchangeInfo);

            if (exchangeInfo is null)
            {
                _logger.LogError("Exchange mapping undefined for '{ChannelType}'", channelTypeName);
                continue;
            }

            ISendEndpoint endpoint = await _bus.GetSendEndpoint(new Uri($"exchange:{exchangeInfo.Exchange}"));

            IEnumerable<PmiReportDistributionMessage> messages = group.Select(item => new PmiReportDistributionMessage(item.PmiReportId));

            await endpoint.SendBatch(messages, ct);
        }
    }
}