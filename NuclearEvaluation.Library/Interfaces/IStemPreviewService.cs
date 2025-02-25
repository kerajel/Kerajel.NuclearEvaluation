using Kerajel.Primitives.Models;

namespace NuclearEvaluation.Library.Interfaces;

public interface IStemPreviewService : IDisposable
{
    Task<OperationResult> DeleteFileData(Guid stemSessionId, int fileId);
    Task<OperationResult> RefreshIndexes(Guid stemSessionId);

    Task<OperationResult<int>> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        string fileName,
        CancellationToken? ct = default);
}