using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement;
using NuclearEvaluation.Kernel.Models.Files;
using Polly;
using Polly.Bulkhead;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewService(
    IStemPreviewParser stemPreviewParser,
    IStemPreviewEntryService stemPreviewEntryService,
    IFileService fileService,
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

    public async Task<OperationResult> EnqueueStemPreviewForProcessingAsync(
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

        WriteFileCommand writeFileCommand = new(fileId, fileName, stream, true);

        try
        {
            result = await bulkheadPolicy.ExecuteAsync(
                async (CancellationToken ct) =>
                {
                    await fileService.Write(writeFileCommand, ct);
                    //upload to temp storate
                    //send to queue
                    return result;
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
    }

    public async Task<OperationResult> UploadStemPreviewFile(
        Guid stemSessionId,
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
                async (CancellationToken ct) =>
                {
                    await Execute();
                    return result;
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
        finally
        {
            // Skipping a full transaction to minimize logging overhead and boost performance
            // In case of an error, we clean up by deleting any file entries that were partially inserted
            if (!result.Succeeded)
            {
                await stemPreviewEntryService.DeleteFileData(stemSessionId, fileId);
            }
        }

        return result;

        async Task<OperationResult> Execute()
        {
            logger.LogInformation("Parsing STEM entries");
            OperationResult<IReadOnlyCollection<StemPreviewEntry>> parseResult = await stemPreviewParser.Parse(stream, fileName, linkedCts.Token);

            if (!parseResult.Succeeded)
            {
                return new(OperationStatus.Faulted, "Error reading the file");
            }

            IReadOnlyCollection<StemPreviewEntry> entries = parseResult.Content!;

            logger.LogInformation("Parsed {stemEntryCount} entries", entries.Count);

            StemPreviewFileMetadata fileMetadata = new() { Id = fileId, Name = fileName };

            await stemPreviewEntryService.InsertStemPreviewFileMetadata(stemSessionId, fileMetadata, linkedCts.Token);

            foreach (StemPreviewEntry entry in entries)
            {
                entry.FileId = fileId;
            }

            await stemPreviewEntryService.InsertStemPreviewEntries(stemSessionId, entries, linkedCts.Token);
            await stemPreviewEntryService.SetStemPreviewFileAsFullyUploaded(stemSessionId, fileId);

            return new(OperationStatus.Succeeded);
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
            await stemPreviewEntryService.DeleteFileData(stemSessionId, fileId); ;
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to delete file data");
            return new OperationResult(OperationStatus.Faulted, ex);
        }
        return new OperationResult(OperationStatus.Succeeded);
    }

    public async ValueTask DisposeAsync()
    {
        _ = stemPreviewEntryService.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}