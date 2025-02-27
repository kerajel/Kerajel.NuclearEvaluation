using Kerajel.Primitives.Models;

namespace NuclearEvaluation.Library.Interfaces;

public interface IStemPreviewService : IDisposable
{
    Task<OperationResult> DeleteFileData(Guid stemSessionId, Guid fileId);
    Task<OperationResult> RefreshIndexes(Guid stemSessionId);

    Task<OperationResult> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        Guid fileId,
        string fileName,
        CancellationToken? ct = default);
}