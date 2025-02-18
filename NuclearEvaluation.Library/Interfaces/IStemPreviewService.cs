using Kerajel.Primitives.Models;

namespace NuclearEvaluation.Library.Interfaces;

public interface IStemPreviewService : IDisposable
{
    Task<OperationResult> UploadStemPreviewFile(
        Guid sessionId,
        Stream stream,
        string fileName,
        CancellationToken? ct = default);
}