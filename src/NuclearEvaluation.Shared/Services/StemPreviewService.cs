using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using Microsoft.Extensions.Logging;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Kernel.Models.Files;
using Polly;
using Polly.Bulkhead;
using System.Runtime.CompilerServices;

namespace NuclearEvaluation.Shared.Services;

public class StemPreviewService(
    IStemPreviewParser stemPreviewParser,
    IStemPreviewEntryService stemPreviewEntryService,
    IEfsFileService efsFileService,
    ILogger<StemPreviewService> logger) : IStemPreviewService
{
    static readonly TimeSpan uploadTimeout = TimeSpan.FromMinutes(5);

    static readonly AsyncBulkheadPolicy<OperationResult> bulkheadPolicy = Policy
        .BulkheadAsync<OperationResult>(
            maxParallelization: 4,
            maxQueuingActions: 128,
            onBulkheadRejectedAsync: async context =>
            {
                await Task.CompletedTask;
            });

    public async Task<OperationResult> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        Guid fileId,
        string fileName,
        CancellationToken? externalCt = default)
    {
        using CancellationTokenSource internalCts = new(uploadTimeout);

        using CancellationTokenSource linkedCts = externalCt.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, externalCt.Value)
            : internalCts;

        OperationResult result = new(OperationStatus.Succeeded);

        try
        {
            result = await bulkheadPolicy.ExecuteAsync(
                async (ct) =>
                {
                    return await Execute();
                },
                linkedCts.Token);
        }
        catch (BulkheadRejectedException ex)
        {
            result = new(OperationStatus.Faulted, "Too many concurrent uploads", ex);
        }
        catch (OperationCanceledException ex)
        {
            result = new(OperationStatus.Faulted, "The upload was canceled or timed out", ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing the file");
            result = new(OperationStatus.Faulted, "Error processing the file", ex);
        }

        return result;

        async Task<OperationResult> Execute()
        {
            WriteFileCommand writeFileCommand = new(fileId, fileName, stream, true);

            OperationResult<FileInfo> writeFileResult = await efsFileService.Write(writeFileCommand, linkedCts.Token);

            if (!writeFileResult.IsSuccessful)
            {
                return OperationResult.Faulted(writeFileResult);
            }

            StemPreviewFileMetadata fileMetadata = new(fileId, fileName);

            await stemPreviewEntryService.InsertStemPreviewFileMetadata(sessionId, fileMetadata, linkedCts.Token);

            using FileStream fs = writeFileResult.Content!.OpenRead();

            IAsyncEnumerable<StemPreviewEntry> asyncEnumerable = stemPreviewParser.Parse(fs, fileName, linkedCts.Token);
            asyncEnumerable = AssignFileId(asyncEnumerable, fileId, linkedCts.Token);

            await stemPreviewEntryService.InsertStemPreviewEntries(sessionId, asyncEnumerable, linkedCts.Token);
            await stemPreviewEntryService.SetStemPreviewFileAsFullyUploaded(sessionId, fileId);

            OperationResult deleteFileResult = await efsFileService.Delete(fileId);

            if (!deleteFileResult.IsSuccessful)
            {
                logger.LogError("Failed to delete file '{fileId}' from the EFS", fileId);
            }

            return new OperationResult(OperationStatus.Succeeded);
        }
    }

    public async Task<OperationResult> RefreshIndexes(Guid stemSessionId)
    {
        try
        {
            await stemPreviewEntryService.RefreshIndexes(stemSessionId);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to refresh indexes");
            return new OperationResult(OperationStatus.Faulted, ex);
        }
        return new OperationResult(OperationStatus.Succeeded);
    }

    public async Task<OperationResult> DeleteFileData(Guid stemSessionId, Guid fileId)
    {
        try
        {
            await stemPreviewEntryService.DeleteFileData(stemSessionId, fileId);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to delete file data");
            return new OperationResult(OperationStatus.Faulted, ex);
        }
        return new OperationResult(OperationStatus.Succeeded);
    }

    static async IAsyncEnumerable<StemPreviewEntry> AssignFileId(
        IAsyncEnumerable<StemPreviewEntry> source,
        Guid fileId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (StemPreviewEntry entry in source.WithCancellation(cancellationToken))
        {
            entry.FileId = fileId;
            yield return entry;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (stemPreviewEntryService != null)
        {
            await stemPreviewEntryService.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}