using Kerajel.Primitives.Models;

namespace NuclearEvaluation.Library.Interfaces;

public interface IStemPreviewService
{
    Task<OperationResult> UploadStemPreviewFile(Stream stream, string fileName, CancellationToken? ct = default);
}