using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using NuclearEvaluation.Server.Services.Files;
using NuclearEvaluation.Shared.Contracts;
using Polly;
using Polly.Bulkhead;

namespace NuclearEvaluation.Server.Services.STEM;

public class StemPreviewService(
    IStemPreviewEntryService stemPreviewEntryService,
    ILogger<StemPreviewService> logger
) : IStemPreviewService
{
    static readonly TimeSpan uploadTimeout = TimeSpan.FromMinutes(5);

    static readonly AsyncBulkheadPolicy<OperationResult> bulkheadPolicy =
        Policy.BulkheadAsync<OperationResult>(maxParallelization: 4, maxQueuingActions: 128);

    public Task<StemFileProfile?> GetFileProfile(
        Guid sessionId,
        Guid fileId,
        CancellationToken ct
    ) => stemPreviewEntryService.GetFileProfile(sessionId, fileId, ct);

    public async Task<OperationResult> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        Guid fileId,
        string fileName,
        CancellationToken? externalCt = default
    )
    {
        using CancellationTokenSource uploadCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(externalCt.GetValueOrDefault());
        uploadCancellation.CancelAfter(uploadTimeout);

        try
        {
            return await bulkheadPolicy.ExecuteAsync(
                async ct =>
                {
                    string safeFileName = SafeFileName.FromClientFileName(fileName, fileId);
                    await stemPreviewEntryService.AppendFileData(
                        sessionId,
                        fileId,
                        safeFileName,
                        stream,
                        ct
                    );
                    return OperationResult.Succeeded();
                },
                uploadCancellation.Token
            );
        }
        catch (BulkheadRejectedException ex)
        {
            return new(OperationStatus.Error, "Too many concurrent uploads", ex);
        }
        catch (OperationCanceledException ex)
        {
            return new(OperationStatus.Error, "The upload was canceled or timed out", ex);
        }
        catch (Exception ex)
        {
            return new(OperationStatus.Error, "Error processing the file", ex);
        }
    }

    public async Task<OperationResult> DeleteFileData(Guid stemSessionId, Guid fileId)
    {
        try
        {
            await stemPreviewEntryService.DeleteFileData(stemSessionId, fileId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete file data");
            return new OperationResult(OperationStatus.Error, ex);
        }
        return new OperationResult(OperationStatus.Succeeded);
    }
}
