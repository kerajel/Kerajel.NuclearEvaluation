using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Shared.Services;

public class PmiReportService : DbServiceBase, IPmiReportService
{
    readonly IEfsFileService _fileService;
    readonly IGuidProvider _guidProvider;

    public PmiReportService(
        NuclearEvaluationServerDbContext dbContext,
        IEfsFileService fileService,
        IGuidProvider guidProvider) : base(dbContext)
    {
        _fileService = fileService;
        _guidProvider = guidProvider;
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

    private PmiReport PreparePmiReport(PmiReportSubmission reportSubmission)
    {
        PmiReport pmiReport = new()
        {
            Name = reportSubmission.ReportName,
            AuthorId = reportSubmission.AuthorId,
            CreatedDate = reportSubmission.ReportDate!.Value,
            Status = PmiReportStatus.Uploaded,
        };

        //OPTIONAL add options to control which channels are active
        foreach (PmiReportDistributionChannel channel in Enum.GetValues<PmiReportDistributionChannel>())
        {
            PmiReportDistributionEntry entry = new()
            {
                PmiReport = pmiReport,
                DistributionChannel = channel,
                DistributionStatus = PmiReportDistributionStatus.Pending,
            };
            pmiReport.PmiReportDistributionEntries.Add(entry);
        }

        PmiReportFileMetadata fileMetadata = new()
        {
            Id = _guidProvider.NewGuid(),
            PmiReport = pmiReport,
            Size = reportSubmission.FileStream.Length,
            FileName = reportSubmission.FileName,
        };

        pmiReport.PmiReportFileMetadata = fileMetadata;

        return pmiReport;
    }
}