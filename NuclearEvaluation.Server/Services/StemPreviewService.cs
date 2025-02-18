using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using Polly;
using Polly.Bulkhead;
using NuclearEvaluation.Library.Models.Temporary;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewService : IStemPreviewService
{
    static readonly AsyncBulkheadPolicy<OperationResult> bulkheadPolicy = Policy
        .BulkheadAsync<OperationResult>(
            maxParallelization: 4,
            maxQueuingActions: 128,
            onBulkheadRejectedAsync: async context =>
            {
                await Task.CompletedTask;
            });

    readonly ITempTableService _tempTableService;
    readonly IStemPreviewParser _stemPreviewParser;

    const string entryTableSuffix = "stem-entry";
    const string fileNameTableSuffix = "stem-file-name";

    public StemPreviewService(ITempTableService tempTableService, IStemPreviewParser stemPreviewParser)
    {
        _tempTableService = tempTableService;
        _stemPreviewParser = stemPreviewParser;
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
        string fileName,
        CancellationToken? externalCt = default)
    {
        //TODO options
        using CancellationTokenSource internalCts = new(TimeSpan.FromMinutes(1));

        using CancellationTokenSource linkedCts = externalCt.HasValue ?
            CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, externalCt.Value) :
            internalCts;

        (string entryTable, string fileNameTable) = GetTableNames(stemSessionId);

        _ = await _tempTableService.EnsureCreated<TempString>(fileNameTable);
        _ = await _tempTableService.EnsureCreated<StemPreviewEntry>(entryTable);

        try
        {
            OperationResult result = await bulkheadPolicy.ExecuteAsync(
                async (CancellationToken ct) =>
                {
                    try
                    {
                        return await Execute();
                    }
                    catch (Exception)
                    {
                        return new OperationResult(OperationStatus.Faulted, "Error processing the file");
                    }
                },
                linkedCts.Token);
            return result;
        }
        catch (BulkheadRejectedException)
        {
            return new OperationResult(OperationStatus.Faulted, "Too many concurrent uploads. Please try again later.");
        }
        catch (OperationCanceledException)
        {
            return new OperationResult(OperationStatus.Faulted, "The upload was canceled or timed out.");
        }

        async Task<OperationResult> Execute()
        {
            OperationResult<IReadOnlyCollection<StemPreviewEntry>> parseResult = await _stemPreviewParser.Parse(stream, fileName);

            if (!parseResult.Succeeded)
            {
                return new(OperationStatus.Faulted, "Error reading the file");
            }

            IReadOnlyCollection<StemPreviewEntry> entries = parseResult.Content!;

            TempString storedFile = new() { Value = fileName };

            int fileId = await _tempTableService.InsertWithIdentity<TempString, int>(fileNameTable, storedFile);

            foreach (StemPreviewEntry entry in entries)
            {
                entry.FileId = fileId;
            }

            await _tempTableService.BulkCopyInto(entryTable, entries);

            return new OperationResult(OperationStatus.Succeeded);
        }
    }

    public (string EntryTable, string FileNameTable) GetTableNames(Guid sessionId)
    {
        return ($"{sessionId}-{entryTableSuffix}", $"{sessionId}-{fileNameTableSuffix}");
    }

    public void Dispose()
    {
        _tempTableService.Dispose();
    }
}