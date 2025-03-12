using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.DataManagement;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IStemPreviewParser
{
    Task<OperationResult<IReadOnlyCollection<StemPreviewEntry>>> Parse(Stream stream, string fileName, CancellationToken ct = default);
}