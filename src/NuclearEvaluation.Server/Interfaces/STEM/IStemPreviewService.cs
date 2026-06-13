using Kerajel.Primitives.Models;

namespace NuclearEvaluation.Server.Interfaces.STEM;

public interface IStemPreviewService
{
    Task<OperationResult> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        Guid fileId,
        string fileName,
        CancellationToken? externalCt = null);

    Task<OperationResult> DeleteFileData(Guid stemSessionId, Guid fileId);
}
