using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.STEM;

/// <summary>
/// Routes STEM staging operations to the per-session throwaway temp tables held by the
/// <see cref="IStemSessionManager"/>.
/// </summary>
public class StemPreviewEntryService : IStemPreviewEntryService
{
    readonly IStemSessionManager _sessionManager;
    readonly ILogger<StemPreviewEntryService> _logger;

    public StemPreviewEntryService(
        IStemSessionManager sessionManager,
        ILogger<StemPreviewEntryService> logger
    )
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public async Task<FetchDataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(
        Guid stemSessionId,
        FetchDataCommand<StemPreviewEntryView> command
    )
    {
        StemSession? session = _sessionManager.TryGet(stemSessionId);
        if (session is null)
        {
            return FetchDataResult<StemPreviewEntryView>.Succeeded([]);
        }
        return await session.QueryViewsAsync(command, command.CancellationToken);
    }

    public Task InsertStemPreviewFileMetadata(
        Guid stemSessionId,
        StemPreviewFileMetadata fileMetadata,
        CancellationToken ct = default
    ) => _sessionManager.GetOrCreate(stemSessionId).InsertFileMetadataAsync(fileMetadata, ct);

    public async Task InsertStemPreviewEntries(
        Guid stemSessionId,
        IAsyncEnumerable<StemPreviewEntry> entries,
        CancellationToken ct = default
    )
    {
        StemSession session = _sessionManager.GetOrCreate(stemSessionId);
        await session.BulkCopyEntriesAsync(entries, ct);

        // The index on the throwaway table is a query optimisation; never fail an upload over it.
        try
        {
            await session.EnsureIndexAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not create index on STEM preview temp table for session {SessionId}.",
                stemSessionId
            );
        }
    }

    public Task SetStemPreviewFileAsFullyUploaded(Guid stemSessionId, Guid fileId) =>
        _sessionManager.GetOrCreate(stemSessionId).SetFileFullyUploadedAsync(fileId);

    public Task DeleteFileData(Guid stemSessionId, Guid fileId) =>
        _sessionManager.TryGet(stemSessionId)?.MarkFileDeletedAsync(fileId) ?? Task.CompletedTask;
}
