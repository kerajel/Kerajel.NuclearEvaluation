using Kerajel.Primitives.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Messages.PMI;
using NuclearEvaluation.Messaging.Interfaces;
using NuclearEvaluation.PmiReportDistributionCoordinator.Interfaces;
using NuclearEvaluation.PmiReportDistributionCoordinator.Models;
using NuclearEvaluation.PmiReportDistributionCoordinator.Models.Settings;

namespace NuclearEvaluation.PmiReportDistributionCoordinator.Jobs;

public partial class EnqueuePmiReportForPublishingJob : IEnqueuePmiReportForPublishingJob
{
    const int maxQueueItemsPerOperation = 3072;

    readonly IMessageDispatcher _pmiReportDistributionMessageDispatcher;
    readonly IPmiReportDistributionService _distributionService;
    readonly ILogger<EnqueuePmiReportForPublishingJob> _logger;
    readonly PmiReportDistributionSettings _pmiReportDistributionSettings;

    public EnqueuePmiReportForPublishingJob(
        IPmiReportDistributionService distributionService,
        ILogger<EnqueuePmiReportForPublishingJob> logger,
        IOptions<PmiReportDistributionSettings> pmiReportDistributionOptions,
        IMessageDispatcher pmiReportDistributionMessageDispatcher)
    {
        _distributionService = distributionService;
        _logger = logger;
        _pmiReportDistributionSettings = pmiReportDistributionOptions.Value;
        _pmiReportDistributionMessageDispatcher = pmiReportDistributionMessageDispatcher;
    }

    public async Task Execute()
    {
        Guid operationId = Guid.NewGuid();

        using IDisposable? scope = _logger.BeginScope("OperationId: {OperationId}", operationId);

        _logger.LogInformation("Starting PMI report distribution process");

        FetchDataResult<PmiReportDistributionQueueItem> fetchItemsResult = await _distributionService.GetQueueItems(maxQueueItemsPerOperation);

        if (!fetchItemsResult.IsSuccessful)
        {
            _logger.LogError(fetchItemsResult.Exception, "Failed to fetch PMI report data for queueing");
            return;
        }

        PmiReportDistributionQueueItem[] distributionEntries = [.. fetchItemsResult.Entries];

        if (distributionEntries.IsNullOrEmpty())
        {
            _logger.LogInformation("No entries found to process");
            return;
        }

        _logger.LogInformation("Dispatching {Count} entries for PMI report distribution", fetchItemsResult.Entries.Count());

        foreach (PmiReportDistributionQueueItem entry in distributionEntries)
        {
            await ProcessEntry(entry);
        }
    }

    async Task ProcessEntry(PmiReportDistributionQueueItem entry)
    {
        string targetChannel = entry.DistributionChannel.ToString();

        _ = _pmiReportDistributionSettings.DistributionMap.TryGetValue(targetChannel, out ExchangeInfo? exchangeInfo);

        if (exchangeInfo is null)
        {
            _logger.LogError("Could not idenfity exchange for {distributuonChannel}", targetChannel);
            return;
        }

        PmiReportDistributionMessage message = new(entry.PmiReportId);

        OperationResult result = await _pmiReportDistributionMessageDispatcher.Send(message, exchangeInfo.Exchange, exchangeInfo.RoutingKey);

        if (!result.IsSuccessful)
        {
            _logger.LogError("Failed to dispatch distribution message for {PmiReportId}", entry.PmiReportId);
            return;
        }

        OperationResult setStatusResult = await _distributionService.SetPmiReportDistributionEntryStatus(PmiReportDistributionStatus.InProgress, entry.PmiReportDistributionEntryId);

        if (setStatusResult.IsSuccessful)
        {
            _logger.LogInformation("Successfully updated status for PMI Report Distribution Entry '{PmiReportDistributionEntryId}'", entry.PmiReportDistributionEntryId);
        }
        else
        {
            _logger.LogError(setStatusResult.Exception, "Failed to update status for PMI Report Distribution Entry '{PmiReportDistributionEntryId}'", entry.PmiReportDistributionEntryId);
        }
    }
}