using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.STEM;

/// <summary>
/// Routes STEM uploads and queries to their preview session.
/// </summary>
public class StemPreviewEntryService(
    IStemSessionManager sessionManager,
    IStemPreviewParser stemPreviewParser
) : IStemPreviewEntryService
{
    public async Task<FetchDataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(
        Guid stemSessionId,
        FetchDataCommand<StemPreviewEntryView> command
    )
    {
        StemSession? session = sessionManager.TryGet(stemSessionId);
        if (session is null)
        {
            return FetchDataResult<StemPreviewEntryView>.Succeeded([]);
        }
        return await session.QueryViewsAsync(command, command.CancellationToken);
    }

    public Task AppendFileData(
        Guid stemSessionId,
        Guid fileId,
        string fileName,
        Stream content,
        CancellationToken ct = default
    ) =>
        sessionManager
            .GetOrCreate(stemSessionId)
            .AppendFileAsync(fileId, fileName, content, stemPreviewParser, ct);

    public Task DeleteFileData(Guid stemSessionId, Guid fileId) =>
        sessionManager.TryGet(stemSessionId)?.DeleteFileAsync(fileId) ?? Task.CompletedTask;
}
