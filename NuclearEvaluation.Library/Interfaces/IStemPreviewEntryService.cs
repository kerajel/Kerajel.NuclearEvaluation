using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.DataManagement;
using NuclearEvaluation.Library.Models.Views;

namespace NuclearEvaluation.Library.Interfaces;

public interface IStemPreviewEntryService : IDisposable
{
    Task<FilterDataResponse<StemPreviewEntryView>> GetStemPreviewEntryViews(Guid stemSessionId, FilterDataCommand<StemPreviewEntryView> command);
    Task DeleteFileData(Guid stemSessionId, int fileId);
    Task<int> InsertStemPreviewFileMetadata(Guid stemSessionId, string fileName);
    Task InsertStemPreviewEntries(Guid stemSessionId, IEnumerable<StemPreviewEntry> entries);
    Task SetStemPreviewFileAsFullyUploaded(Guid stemSessionId, int fileId);
    Task RefreshIndexes(Guid stemSessionId);
}