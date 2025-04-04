using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Kernel.Models.Files;
using NuclearEvaluation.Server.Interfaces.EFS;
using NuclearEvaluation.Server.Interfaces.PMI;

namespace NuclearEvaluation.Server.Services.PMI;

public class PmiReportUploadService : IPmiReportUploadService
{
    readonly IEfsFileService _fileService;
    readonly IPmiReportService _pmiReportService;
    readonly ILogger<PmiReportUploadService> _logger;

    public PmiReportUploadService(
        IEfsFileService fileService,
        IPmiReportService pmiReportService,
        ILogger<PmiReportUploadService> logger)
    {
        _fileService = fileService;
        _pmiReportService = pmiReportService;
        _logger = logger;
    }

    public async Task<OperationResult<PmiReport>> Upload(PmiReportSubmission reportSubmission, CancellationToken ct = default)
    {
        try
        {
            OperationResult<PmiReport> createReportResult = await _pmiReportService.Create(reportSubmission, ct);

            if (!createReportResult.IsSuccessful)
            {
                return OperationResult<PmiReport>.Faulted(createReportResult);
            }

            PmiReport pmiReport = createReportResult.Content!;

            Guid fileId = pmiReport.PmiReportFileMetadata.Id;
            string fileName = pmiReport.PmiReportFileMetadata.FileName;
            Stream content = reportSubmission.FileStream;

            WriteFileCommand writeFileCommand = new(fileId, fileName, content);

            OperationResult<FileInfo> writeFileResult = await _fileService.Write(writeFileCommand, ct);

            if (!writeFileResult.IsSuccessful)
            {
                await TryDeletePmiReport(pmiReport.Id, ct);
                return OperationResult<PmiReport>.Faulted(writeFileResult);
            }

            return OperationResult<PmiReport>.Succeeded(pmiReport);
        }
        catch (Exception ex)
        {
            return OperationResult<PmiReport>.Faulted(ex);
        }
    }

    private async Task TryDeletePmiReport(Guid pmiReportId, CancellationToken ct = default)
    {
        OperationResult deletePmiReportResult = await _pmiReportService.Delete(pmiReportId, ct);
        if (!deletePmiReportResult.IsSuccessful)
        {
            _logger.LogError("Failed to delete PMI Report {pmiReportId}", pmiReportId);

        }
    }
}