using NuclearEvaluation.HangfireJobs.Models;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.HangfireJobs.Services;

//TODO rename
public class PmiReportEmailChannelMessager
{
    //TODO DI
    readonly Dictionary<PmiReportDistributionChannel, string> _queueMapping;
    readonly IMessager _messager;

    public PmiReportEmailChannelMessager(IMessager messager)
    {
        _messager = messager;
    }
    public bool CanHandle(PmiReportDistributionChannel channel) => channel == PmiReportDistributionChannel.Email;

    public async Task Send(IEnumerable<PmiReportDistributionQueueItem> queueItems, CancellationToken? ct = default)
    {
        foreach (PmiReportDistributionQueueItem item in queueItems)
        {
            await _messager.PublishMessageAsync()
        }
    }
}
