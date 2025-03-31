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
        FetchDataResult<PmiReportDistributionQueueItem> fetchItemsResult = await _distributionService.GetQueueItems(maxQueueItemsPerOperation);

        if (!fetchItemsResult.IsSuccessful)
        {
            _logger.LogError(fetchItemsResult.Exception, "Failed to fetch PMI report data for queueing");
            return;
        }

        if (fetchItemsResult.Entries.IsNullOrEmpty())
        {
            return;
        }

        await _pmiReportDistributionMessageDispatcher.Send(fetchItemsResult.Entries);

        PmiReportDistributionStatus inProgressStatus = PmiReportDistributionStatus.InProgress;
        IEnumerable<int> distributionItemIds = fetchItemsResult.Entries.Select(x => x.PmiReportDistributionEntryId);

        OperationResult setStatusResult = await _distributionService.SetPmiReportDistributionEntryStatus(inProgressStatus, distributionItemIds);

        if (!setStatusResult.IsSuccessful)
        {
            _logger.LogError(setStatusResult.Exception, "Failed to update status of PMI report distributiion entries to {status}", inProgressStatus);
        }
    }
}