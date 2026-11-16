using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.STEM;

public interface IStemPreviewEntryService
{
    Task<FetchDataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(
        Guid stemSessionId,
        FetchDataCommand<StemPreviewEntryView> command
    );
    Task DeleteFileData(Guid stemSessionId, Guid fileId);
    Task AppendFileData(
        Guid stemSessionId,
        Guid fileId,
        string fileName,
        Stream content,
        CancellationToken ct = default
    );
}
