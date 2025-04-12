using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Server.Pages;

public class DownloadPmiReportModel : PageModel
{
    private readonly ILogger<DownloadPmiReportModel> _logger;
    readonly IPmiReportService _pmiReportService;
    readonly IEfsFileService _efsFileService;

    public DownloadPmiReportModel(
        ILogger<DownloadPmiReportModel> logger,
        IEfsFileService efsFileService,
        IPmiReportService pmiReportService)
    {
        _logger = logger;
        _efsFileService = efsFileService;
        _pmiReportService = pmiReportService;
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct = default)
    {
        FetchDataCommand<PmiReportView> command = new()
        {
            TopLevelFilterExpression = x => x.Id == id
        };

        FetchDataResult<PmiReportView> fetchPmiReportViewResult = await _pmiReportService.GetPmiReportViews(command, ct);

        if (!fetchPmiReportViewResult.IsSuccessful)
        {
            if (fetchPmiReportViewResult.NotFound)
            {
                _logger.LogError("Could not find PMI Report under id {pmiReportId}", id);
                return StatusCode(StatusCodes.Status404NotFound, "PMI Report not found");
            }

            _logger.LogError("Error retrieving PMI Report under id {pmiReportId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred while retrieving the file");
        }

        PmiReportView pmiReport = fetchPmiReportViewResult.Entries.Single();
        PmiReportFileMetadataView fileMetadata = pmiReport.FileMetadata;
        Guid fileId = fileMetadata.Id;

        OperationResult<FileInfo> getFileInfoResult = await _efsFileService.GetFileInfo(fileId, ct);

        if (!getFileInfoResult.IsSuccessful)
        {
            if (getFileInfoResult.NotFound)
            {
                _logger.LogError("Could not find PMI Report file under id {pmiReportFileId}", fileId);
                return StatusCode(StatusCodes.Status404NotFound, "PMI Report file not found");
            }

            _logger.LogError("Error retrieving PMI Report file under id {pmiReportFileId}", fileId);
            return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred while retrieving the file");
        }

        Stream reportStream = getFileInfoResult.Content!.OpenRead();
        string fileName = getFileInfoResult.Content.Name;

        return File(reportStream, "application/octet-stream", fileName);
    }
}

public class DownloadPmiReportViewModel : PageModel
{
    public Guid ReportId { get; set; }

    public void OnGet(Guid id)
    {
        ReportId = id;
    }
}