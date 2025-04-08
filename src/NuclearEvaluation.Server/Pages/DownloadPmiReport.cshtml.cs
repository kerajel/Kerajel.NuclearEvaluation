using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NuclearEvaluation.Server.Pages;

public class DownloadPmiReportModel : PageModel
{
    private readonly ILogger<DownloadPmiReportModel> _logger;
    readonly IEfsFileService _efsFileService;

    public DownloadPmiReportModel(
        ILogger<DownloadPmiReportModel> logger,
        IEfsFileService efsFileService)
    {
        _logger = logger;
        _efsFileService = efsFileService;
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        OperationResult<FileInfo> getFileInfoResult = await _efsFileService.GetFileInfo(id);

        if (!getFileInfoResult.IsSuccessful)
        {
            _logger.LogError("Could not find PMI report under id {pmiReportId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, "Error occurred while retrieving the file.");

        }

        using Stream reportStream = getFileInfoResult.Content!.OpenRead();
        string fileName = getFileInfoResult.Content.Name;
        
        return File(reportStream, fileName, "application/octet-stream");
    }
}