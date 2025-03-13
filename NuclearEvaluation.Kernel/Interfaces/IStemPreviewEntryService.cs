using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IStemPreviewEntryService : IAsyncDisposable
{
    Task<FilterDataResponse<StemPreviewEntryView>> GetStemPreviewEntryViews(Guid stemSessionId, FilterDataCommand<StemPreviewEntryView> command);
    Task DeleteFileData(Guid stemSessionId, Guid fileId);
    Task InsertStemPreviewFileMetadata(Guid stemSessionId, StemPreviewFileMetadata fileMetadata, CancellationToken ct = default);
    Task InsertStemPreviewEntries(Guid stemSessionId, IEnumerable<StemPreviewEntry> entries, CancellationToken ct = default);
    Task SetStemPreviewFileAsFullyUploaded(Guid stemSessionId, Guid fileId);
    Task RefreshIndexes(Guid stemSessionId);
}