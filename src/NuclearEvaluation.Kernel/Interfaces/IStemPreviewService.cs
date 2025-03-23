using Kerajel.Primitives.Models;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IStemPreviewService : IAsyncDisposable
{
    Task<OperationResult> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        Guid fileId,
        string fileName,
        CancellationToken? externalCt = null);

    Task<OperationResult> RefreshIndexes(Guid stemSessionId);
    Task<OperationResult> DeleteFileData(Guid stemSessionId, Guid fileId);

}