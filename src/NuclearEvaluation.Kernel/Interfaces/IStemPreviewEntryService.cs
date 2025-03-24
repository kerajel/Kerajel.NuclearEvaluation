using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IStemPreviewEntryService : IAsyncDisposable
{
    Task<FilterDataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(Guid stemSessionId, FilterDataCommand<StemPreviewEntryView> command);
    Task DeleteFileData(Guid stemSessionId, Guid fileId);
    Task InsertStemPreviewFileMetadata(Guid stemSessionId, StemPreviewFileMetadata fileMetadata, CancellationToken ct = default);
    Task InsertStemPreviewEntries(Guid stemSessionId, IAsyncEnumerable<StemPreviewEntry> entries, CancellationToken ct = default);
    Task SetStemPreviewFileAsFullyUploaded(Guid stemSessionId, Guid fileId);
    Task RefreshIndexes(Guid stemSessionId);
}