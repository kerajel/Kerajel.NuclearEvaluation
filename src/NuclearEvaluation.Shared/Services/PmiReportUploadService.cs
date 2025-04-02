using Kerajel.Primitives.Models;
using LinqToDB;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Kernel.Models.Files;

namespace NuclearEvaluation.Shared.Services;

public class PmiReportUploadService
{
    readonly IEfsFileService _fileService;
    readonly IPmiReportService _pmiReportService;

    public PmiReportUploadService(
        IEfsFileService fileService,
        IPmiReportService pmiReportService)
    {
        _fileService = fileService;
        _pmiReportService = pmiReportService;
    }

    public async Task<OperationResult<PmiReport>> Create(PmiReportSubmission reportSubmission, CancellationToken ct = default)
    {
        try
        {
            OperationResult<PmiReport> createReportResult = await _pmiReportService.Create(reportSubmission);

            if (!createReportResult.IsSuccessful)
            {
                return OperationResult<PmiReport>.Faulted(createReportResult);
            }

            PmiReport pmiReport = createReportResult.Content!;

            Guid fileId = pmiReport.PmiReportFileMetadata.Id;
            string fileName = pmiReport.PmiReportFileMetadata.FileName;
            Stream content = reportSubmission.FileStream;

            WriteFileCommand writeFileCommand = new(fileId, fileName, content);

            //TODO finish class
            await _fileService.Write(writeFileCommand, ct);

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

        return pmiReport;
    }
}