using Kerajel.Primitives.Models;
using NuclearEvaluation.HangfireJobs.Interfaces;
using NuclearEvaluation.HangfireJobs.Models;
using NuclearEvaluation.HangfireJobs.Services;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.HangfireJobs.Jobs;

public partial class EnqueueStemReportForPublishingJob : IEnqueueStemReportForPublishingJob
{
    const int maxQueueItemsPerOperation = 3072;

    readonly IPmiReportDistributionService _distributionService;
    readonly ILogger<EnqueueStemReportForPublishingJob> _logger;

    public EnqueueStemReportForPublishingJob(
        IPmiReportDistributionService distributionService,
        ILogger<EnqueueStemReportForPublishingJob> logger)
    {
        _distributionService = distributionService;
        _logger = logger;
    }

    public async Task Execute()
    {
        FetchDataResult<PmiReportDistributionQueueItem> fetchItemsResult = await _distributionService.GetQueueItems(maxQueueItemsPerOperation);

        if (!fetchItemsResult.IsSuccessful)
        {
            _logger.LogError(fetchItemsResult.Exception, "Failed to fetch PMI report data for queueing");
        }

        //TODO dispatch messages

        PmiReportDistributionStatus inProgressStatus = PmiReportDistributionStatus.InProgress;
        IEnumerable<int> distributionItemIds = fetchItemsResult.Entries.Select(x => x.PmiReportDistributionEntryId);

        OperationResult setStatusResult = await _distributionService.SetPmiReportDistributionEntryStatus(inProgressStatus, distributionItemIds);

        if (!setStatusResult.IsSuccessful)
        {
            _logger.LogError(setStatusResult.Exception, "Failed to update status of PMI report distributiion entries to {status}", inProgressStatus);
        }
    }
}