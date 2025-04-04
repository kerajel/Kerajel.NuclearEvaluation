using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.STEM;

public interface IStemPreviewEntryService : IAsyncDisposable
{
    Task<FetchDataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(Guid stemSessionId, FetchDataCommand<StemPreviewEntryView> command);
    Task DeleteFileData(Guid stemSessionId, Guid fileId);
    Task InsertStemPreviewFileMetadata(Guid stemSessionId, StemPreviewFileMetadata fileMetadata, CancellationToken ct = default);
    Task InsertStemPreviewEntries(Guid stemSessionId, IAsyncEnumerable<StemPreviewEntry> entries, CancellationToken ct = default);
    Task SetStemPreviewFileAsFullyUploaded(Guid stemSessionId, Guid fileId);
    Task RefreshIndexes(Guid stemSessionId);
}