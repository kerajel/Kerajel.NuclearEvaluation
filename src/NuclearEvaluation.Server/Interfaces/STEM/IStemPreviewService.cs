using Kerajel.Primitives.Models;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Interfaces.STEM;

public interface IStemPreviewService
{
    Task<StemFileProfile?> GetFileProfile(Guid sessionId, Guid fileId, CancellationToken ct);

    Task<OperationResult> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        Guid fileId,
        string fileName,
        CancellationToken? externalCt = null
    );

    Task<OperationResult> DeleteFileData(Guid stemSessionId, Guid fileId);
}
