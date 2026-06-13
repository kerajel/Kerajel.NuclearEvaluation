using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Server.Interfaces.EFS;
using NuclearEvaluation.Server.Interfaces.PMI;
using NuclearEvaluation.Server.Services.Sandbox;
using NuclearEvaluation.Shared;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/pmi-reports")]
public class PmiReportsController : ControllerBase
{
    readonly IPmiReportUploadService _uploadService;
    readonly IPmiReportService _pmiReportService;
    readonly IEfsFileService _efsFileService;
    readonly IStorageQuotaService _storageQuotaService;
    readonly ILogger<PmiReportsController> _logger;

    public PmiReportsController(
        IPmiReportUploadService uploadService,
        IPmiReportService pmiReportService,
        IEfsFileService efsFileService,
        IStorageQuotaService storageQuotaService,
        ILogger<PmiReportsController> logger)
    {
        _uploadService = uploadService;
        _pmiReportService = pmiReportService;
        _efsFileService = efsFileService;
        _storageQuotaService = storageQuotaService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Uploads)]
    [RequestSizeLimit(UploadLimits.MaxPmiReportFileSizeBytes + 8192)]
    public async Task<OperationOutcome> Upload(
        [FromForm] string reportName,
        [FromForm] DateOnly reportDate,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return OperationOutcome.Fail("No file was provided.");
        }
        if (file.Length > UploadLimits.MaxPmiReportFileSizeBytes)
        {
            return OperationOutcome.Fail("File exceeds the size limit.");
        }
        if (!file.FileName.EndsWith(UploadLimits.PmiReportExtension, StringComparison.OrdinalIgnoreCase))
        {
            return OperationOutcome.Fail("File must be a .docx document.");
        }
        if (!_storageQuotaService.CanAccept(file.Length))
        {
            return OperationOutcome.Fail("The site has reached its storage limit. Please try again later.");
        }

        await using Stream stream = file.OpenReadStream();
        OperationResult<NuclearEvaluation.Kernel.Models.DataManagement.PMI.PmiReport> result =
            await _uploadService.Upload(reportName, reportDate, file.FileName, stream, ct);

        _storageQuotaService.Invalidate();

        return result.IsSuccessful
            ? OperationOutcome.Ok()
            : OperationOutcome.Fail("The report could not be saved.");
    }

    [HttpGet("name-available")]
    public async Task<bool> NameAvailable([FromQuery] string name, CancellationToken ct)
        => await _pmiReportService.IsNameAvailable(name, ct);

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        FetchDataCommand<PmiReportView> command = new()
        {
            TopLevelFilterExpression = x => x.Id == id,
        };

        FetchDataResult<PmiReportView> fetchResult = await _pmiReportService.GetPmiReportViews(command, ct);

        if (!fetchResult.IsSuccessful)
        {
            return fetchResult.NotFound ? NotFound("PMI Report not found") : StatusCode(500);
        }

        PmiReportView report = fetchResult.Entries.Single();
        Guid fileId = report.FileMetadata.Id;

        OperationResult<FileInfo> fileResult = await _efsFileService.GetFileInfo(fileId, ct);
        if (!fileResult.IsSuccessful)
        {
            return fileResult.NotFound ? NotFound("PMI Report file not found") : StatusCode(500);
        }

        Stream reportStream = fileResult.Content!.OpenRead();
        return File(reportStream, "application/octet-stream", fileResult.Content.Name);
    }
}
