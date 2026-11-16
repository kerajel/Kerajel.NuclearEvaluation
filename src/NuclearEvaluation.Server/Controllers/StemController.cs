using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NuclearEvaluation.Server.Services.Sandbox;
using NuclearEvaluation.Shared;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/stem")]
public class StemController(
    IStemPreviewService stemPreviewService,
    IStorageQuotaService storageQuotaService,
    StemUploadLog uploadLog
) : ControllerBase
{
    [HttpPost("{sessionId:guid}/files")]
    [EnableRateLimiting(RateLimitPolicies.Uploads)]
    [RequestSizeLimit(UploadLimits.MaxStemPreviewFileSizeBytes + 8192)]
    [ServiceFilter<StemUploadLog>]
    public async Task<OperationOutcome> Upload(
        Guid sessionId,
        [FromForm] Guid fileId,
        IFormFile file,
        CancellationToken ct
    )
    {
        uploadLog.FileId = fileId == Guid.Empty ? null : fileId;
        uploadLog.FileBytes = file?.Length;
        if (file is null || file.Length == 0)
        {
            uploadLog.Rejection = "missing_file";
            return OperationOutcome.Fail("No file was provided.");
        }
        if (file.Length > UploadLimits.MaxStemPreviewFileSizeBytes)
        {
            uploadLog.Rejection = "size_limit";
            return OperationOutcome.Fail("File exceeds the size limit.");
        }
        if (!storageQuotaService.CanAccept(file.Length))
        {
            uploadLog.Rejection = "storage_limit";
            return OperationOutcome.Fail(
                "The site has reached its storage limit. Please try again later."
            );
        }

        // The client supplies the file id so it can later delete exactly this file's staged rows.
        if (fileId == Guid.Empty)
        {
            fileId = Guid.NewGuid();
        }
        uploadLog.FileId = fileId;

        await using Stream stream = file.OpenReadStream();
        OperationResult result = await stemPreviewService.UploadStemPreviewFile(
            sessionId,
            stream,
            fileId,
            file.FileName,
            ct
        );

        uploadLog.ImportError = result.Exception;
        storageQuotaService.Invalidate();

        return result.IsSuccessful
            ? OperationOutcome.Ok()
            : OperationOutcome.Fail(result.ErrorMessage);
    }

    [HttpDelete("{sessionId:guid}/files/{fileId:guid}")]
    public async Task<IActionResult> Delete(Guid sessionId, Guid fileId)
    {
        await stemPreviewService.DeleteFileData(sessionId, fileId);
        storageQuotaService.Invalidate();
        return Ok();
    }
}
