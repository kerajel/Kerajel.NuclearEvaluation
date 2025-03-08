using Kerajel.Primitives.Models;
using NuclearEvaluation.Library.Models.DataManagement;

namespace NuclearEvaluation.Library.Interfaces;

public interface IStemPreviewParser
{
    Task<OperationResult<IReadOnlyCollection<StemPreviewEntry>>> Parse(Stream stream, string fileName, CancellationToken ct = default);
}