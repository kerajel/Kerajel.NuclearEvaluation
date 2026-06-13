using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NuclearEvaluation.Server.Interfaces.STEM;
using NuclearEvaluation.Server.Services.Sandbox;
using NuclearEvaluation.Shared;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/stem")]
public class StemController : ControllerBase
{
    readonly IStemPreviewService _stemPreviewService;
    readonly IStorageQuotaService _storageQuotaService;

    public StemController(IStemPreviewService stemPreviewService, IStorageQuotaService storageQuotaService)
    {
        _stemPreviewService = stemPreviewService;
        _storageQuotaService = storageQuotaService;
    }

    [HttpPost("{sessionId:guid}/files")]
    [EnableRateLimiting(RateLimitPolicies.Uploads)]
    [RequestSizeLimit(UploadLimits.MaxStemPreviewFileSizeBytes + 8192)]
    public async Task<OperationOutcome> Upload(Guid sessionId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return OperationOutcome.Fail("No file was provided.");
        }
        if (file.Length > UploadLimits.MaxStemPreviewFileSizeBytes)
        {
            return OperationOutcome.Fail("File exceeds the size limit.");
        }
        if (!_storageQuotaService.CanAccept(file.Length))
        {
            return OperationOutcome.Fail("The site has reached its storage limit. Please try again later.");
        }

        Guid fileId = Guid.NewGuid();
        await using Stream stream = file.OpenReadStream();
        OperationResult result = await _stemPreviewService.UploadStemPreviewFile(sessionId, stream, fileId, file.FileName, ct);

        _storageQuotaService.Invalidate();

        return result.IsSuccessful
            ? OperationOutcome.Ok()
            : OperationOutcome.Fail(result.ErrorMessage);
    }

    [HttpDelete("{sessionId:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> Delete(Guid sessionId, Guid fileId)
    {
        await _stemPreviewService.DeleteFileData(sessionId, fileId);
        return Ok();
    }
}
