using System.Runtime.CompilerServices;
using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.Files;
using NuclearEvaluation.Server.Services.Files;
using Polly;
using Polly.Bulkhead;

namespace NuclearEvaluation.Server.Services.STEM;

public class StemPreviewService(
    IStemPreviewParser stemPreviewParser,
    IStemPreviewEntryService stemPreviewEntryService,
    IEfsFileService efsFileService,
    ILogger<StemPreviewService> logger
) : IStemPreviewService
{
    static readonly TimeSpan uploadTimeout = TimeSpan.FromMinutes(5);

    static readonly AsyncBulkheadPolicy<OperationResult> bulkheadPolicy =
        Policy.BulkheadAsync<OperationResult>(
            maxParallelization: 4,
            maxQueuingActions: 128,
            onBulkheadRejectedAsync: async context =>
            {
                await Task.CompletedTask;
            }
        );

    public async Task<OperationResult> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        Guid fileId,
        string fileName,
        CancellationToken? externalCt = default
    )
    {
        using CancellationTokenSource internalCts = new(uploadTimeout);

        using CancellationTokenSource linkedCts = externalCt.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, externalCt.Value)
            : internalCts;

        OperationResult result;

        try
        {
            result = await bulkheadPolicy.ExecuteAsync(
                async (ct) => await Execute(),
                linkedCts.Token
            );
        }
        catch (BulkheadRejectedException ex)
        {
            result = new(OperationStatus.Error, "Too many concurrent uploads", ex);
        }
        catch (OperationCanceledException ex)
        {
            result = new(OperationStatus.Error, "The upload was canceled or timed out", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing the file");
            result = new(OperationStatus.Error, "Error processing the file", ex);
        }

        return result;

        async Task<OperationResult> Execute()
        {
            string safeFileName = SafeFileName.FromClientFileName(fileName, fileId);
            bool metadataInserted = false;
            try
            {
                WriteFileCommand command = new(fileId, safeFileName, stream, true);
                OperationResult<FileInfo> written = await efsFileService.Write(
                    command,
                    linkedCts.Token
                );
                if (!written.IsSuccessful)
                    return OperationResult.Faulted(written);

                await stemPreviewEntryService.InsertStemPreviewFileMetadata(
                    sessionId,
                    new(fileId, safeFileName),
                    linkedCts.Token
                );
                metadataInserted = true;
                using FileStream fs = written.Content!.OpenRead();
                IAsyncEnumerable<StemPreviewEntry> entries = stemPreviewParser.Parse(
                    fs,
                    safeFileName,
                    linkedCts.Token
                );
                await stemPreviewEntryService.InsertStemPreviewEntries(
                    sessionId,
                    AssignFileId(entries, fileId, linkedCts.Token),
                    linkedCts.Token
                );
                linkedCts.Token.ThrowIfCancellationRequested();
                await stemPreviewEntryService.SetStemPreviewFileAsFullyUploaded(sessionId, fileId);
                return OperationResult.Succeeded();
            }
            catch
            {
                // Bulk copy can commit earlier batches before a later row fails.
                if (metadataInserted)
                    await DeleteFileData(sessionId, fileId);
                throw;
            }
            finally
            {
                // The upload stream is disposed before deleting the staged file.
                // Clean up partial writes and failed parses even after cancellation.
                OperationResult deleted = await efsFileService.Delete(fileId);
                if (!deleted.IsSuccessful)
                {
                    logger.LogWarning("Failed to delete staged file {FileId}", fileId);
                }
            }
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

    static async IAsyncEnumerable<StemPreviewEntry> AssignFileId(
        IAsyncEnumerable<StemPreviewEntry> source,
        Guid fileId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        await foreach (StemPreviewEntry entry in source.WithCancellation(cancellationToken))
        {
            entry.FileId = fileId;
            yield return entry;
        }
    }
}
