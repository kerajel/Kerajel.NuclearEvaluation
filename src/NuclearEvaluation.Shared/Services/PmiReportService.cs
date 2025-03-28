using Kerajel.Primitives.Models;
using LinqToDB;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Shared.Services;

public class PmiReportService : DbServiceBase, IPmiReportService
{
    public PmiReportService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {

    }

    public async Task<OperationResult<PmiReport>> Create(PmiReportSubmission reportSubmission)
    {
        try
        {
            PmiReport pmiReport = PreparePmiReport(reportSubmission);
            _dbContext.Add(pmiReport);
            await _dbContext.SaveChangesAsync();
            return OperationResult<PmiReport>.Succeeded(pmiReport);
        }
        catch (Exception ex)
        {
            return OperationResult<PmiReport>.Faulted(ex);
        }
    }

    private static PmiReport PreparePmiReport(PmiReportSubmission reportSubmission)
    {
        PmiReport pmiReport = new()
        {
            Name = reportSubmission.ReportName,
            AuthorId = reportSubmission.AuthorId,
            CreatedDate = reportSubmission.ReportDate!.Value,
            Status = PmiReportStatus.Uploaded,
        };

        //TODO add options to control which channels are active
        foreach (PmiReportDistributionChannel channel in Enum.GetValues<PmiReportDistributionChannel>())
        {
            PmiReportDistributionEntry entry = new()
            {
                PmiReport = pmiReport,
                PmiReportDistributionChannel = channel,
                PmiReportDistributionStatus = PmiReportDistributionStatus.Pending,
            };
            pmiReport.PmiReportDistributionEntries.Add(entry);
        }

        return pmiReport;
    }
}