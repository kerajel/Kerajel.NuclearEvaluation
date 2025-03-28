using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.HangfireJobs.Services;

public class PmiReportEmailChannelMessager
{

    public bool CanHandle(PmiReportDistributionChannel channel) => channel == PmiReportDistributionChannel.Email;

    public async Task SendMessages()
    {

    }
}
