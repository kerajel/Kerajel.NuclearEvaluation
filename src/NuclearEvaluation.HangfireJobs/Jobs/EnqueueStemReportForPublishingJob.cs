using Kerajel.Primitives.Models;
using Microsoft.IdentityModel.Tokens;
using NuclearEvaluation.HangfireJobs.Interfaces;
using NuclearEvaluation.HangfireJobs.Models;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.HangfireJobs.Jobs;

public partial class EnqueueStemReportForPublishingJob : IEnqueueStemReportForPublishingJob
{
    const int maxQueueItemsPerOperation = 3072;

    readonly IPmiReportDistributionMessageDispatcher _pmiReportDistributionMessageDispatcher;
    readonly IPmiReportDistributionService _distributionService;
    readonly ILogger<EnqueueStemReportForPublishingJob> _logger;

    public EnqueueStemReportForPublishingJob(
        IPmiReportDistributionService distributionService,
        ILogger<EnqueueStemReportForPublishingJob> logger,
        IPmiReportDistributionMessageDispatcher pmiReportDistributionMessageDispatcher)
    {
        _distributionService = distributionService;
        _logger = logger;
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

        if (fetchItemsResult.Entries.IsNullOrEmpty())
        {
            _logger.LogInformation("No entries found to process");
            return;
        }

        _logger.LogInformation("Dispatching {Count} entries for PMI report distribution", fetchItemsResult.Entries.Count());

        await _pmiReportDistributionMessageDispatcher.Send(fetchItemsResult.Entries);

        PmiReportDistributionStatus inProgressStatus = PmiReportDistributionStatus.InProgress;
        IEnumerable<int> distributionItemIds = fetchItemsResult.Entries.Select(x => x.PmiReportDistributionEntryId);

        _logger.LogInformation("Setting PMI report distribution entry status to {Status}", inProgressStatus);

        OperationResult setStatusResult = await _distributionService.SetPmiReportDistributionEntryStatus(inProgressStatus, distributionItemIds);

        if (setStatusResult.IsSuccessful)
        {
            _logger.LogInformation("Successfully updated status for all entries");
        }
        else
        {
            _logger.LogError(setStatusResult.Exception, "Failed to update status of PMI report distribution entries to {Status}", inProgressStatus);
        }
    }
}