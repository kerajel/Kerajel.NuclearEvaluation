using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using Polly;
using Polly.Bulkhead;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewService : IStemPreviewService
{
    static readonly TimeSpan uploadTimeout = TimeSpan.FromMinutes(1);

    static readonly AsyncBulkheadPolicy<OperationResult> bulkheadPolicy = Policy
        .BulkheadAsync<OperationResult>(
            maxParallelization: 4,
            maxQueuingActions: 128,
            onBulkheadRejectedAsync: async context =>
            {
                await Task.CompletedTask;
            });

    readonly IStemPreviewParser _stemPreviewParser;
    readonly IStemPreviewEntryService _stemPreviewEntryService;
    readonly ILogger<StemPreviewService> _logger;

    public StemPreviewService(
        IStemPreviewParser stemPreviewParser,
        IStemPreviewEntryService stemPreviewEntryService,
        ILogger<StemPreviewService> logger)
    {
        _stemPreviewParser = stemPreviewParser;
        _stemPreviewEntryService = stemPreviewEntryService;
        _logger = logger;
    }

    // TODO: Optimize memory usage by implementing file streaming.
    // The ideal approach involves streaming the input file to a physical temporary file,
    // then having RUST process this file and output to another temporary file.
    // Subsequently, CSVHelper should read this output file in chunks, streaming parsed entries directly to the database.
    // This method conserves memory by avoiding in-memory processing of the entire file at once.
    // For simplicity, the current implementation handles everything in memory.
    public async Task<OperationResult> UploadStemPreviewFile(
        Guid stemSessionId,
        Stream stream,
        Guid fileId,
        string fileName,
        CancellationToken? externalCt = default)
    {
        using CancellationTokenSource internalCts = new(uploadTimeout);

        using CancellationTokenSource linkedCts = externalCt.HasValue ?
            CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, externalCt.Value) :
            internalCts;

        OperationResult result = new(OperationStatus.Succeeded);
        
        try
        {
            result = await bulkheadPolicy.ExecuteAsync(
                async (CancellationToken ct) =>
                {
                    try
                    {
                        await Execute();
                        return result;
                    }
                    catch (Exception ex)
                    {
                        return new (OperationStatus.Faulted, "Error processing the file", ex);
                    }
                },
                linkedCts.Token);
        }
        catch (BulkheadRejectedException ex)
        {
            result = new (OperationStatus.Faulted, "Too many concurrent uploads. Please try again later", ex);
        }
        catch (OperationCanceledException ex)
        {
            result = new (OperationStatus.Faulted, "The upload was canceled or timed out", ex);
        }
        catch (Exception ex)
        {
            result = new (OperationStatus.Faulted, "Error processing the file", ex);
        }
        finally
        {
            // Skipping a full transaction to minimize logging overhead and boost performance
            // In case of an error, we clean up by deleting any file entries that were partially inserted
            if (!result.Succeeded)
            {
                await _stemPreviewEntryService.DeleteFileData(stemSessionId, fileId);
            }
        }
        
        return result;

        async Task<OperationResult> Execute()
        {
            OperationResult<IReadOnlyCollection<StemPreviewEntry>> parseResult = await _stemPreviewParser.Parse(stream, fileName, linkedCts.Token);

            if (!parseResult.Succeeded)
            {
                return new(OperationStatus.Faulted, "Error reading the file");
            }

            StemPreviewFileMetadata fileMetadata = new() { Id = fileId, Name = fileName };

            await _stemPreviewEntryService.InsertStemPreviewFileMetadata(stemSessionId, fileMetadata, linkedCts.Token);

            IReadOnlyCollection<StemPreviewEntry> entries = parseResult.Content!;

            foreach (StemPreviewEntry entry in entries)
            {
                entry.FileId = fileId;
            }

            await _stemPreviewEntryService.InsertStemPreviewEntries(stemSessionId, entries, linkedCts.Token);
            await _stemPreviewEntryService.SetStemPreviewFileAsFullyUploaded(stemSessionId, fileId);

            return new (OperationStatus.Succeeded);
        }
    }

    public async Task<OperationResult> RefreshIndexes(Guid stemSessionId)
    {
        try
        {
            await _stemPreviewEntryService.RefreshIndexes(stemSessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to refresh indexes for Stem Preview session {sessionId}", stemSessionId);
            return new OperationResult(OperationStatus.Faulted, ex);
        }
        return new OperationResult(OperationStatus.Succeeded);
    }

    public async Task<OperationResult> DeleteFileData(Guid stemSessionId, Guid fileId)
    {
        try
        {
            await _stemPreviewEntryService.DeleteFileData(stemSessionId, fileId); ;
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to delete data for file {fileId}, Stem Preview session {sessionId}", fileId, stemSessionId);
            return new OperationResult(OperationStatus.Faulted, ex);
        }
        return new OperationResult(OperationStatus.Succeeded);
    }

    public void Dispose()
    {
        _stemPreviewEntryService.Dispose();
        GC.SuppressFinalize(this);
    }
}