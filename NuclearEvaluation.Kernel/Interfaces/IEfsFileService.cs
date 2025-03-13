using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.Files;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IEfsFileService
{
    Task<OperationResult> Write(WriteFileCommand command, CancellationToken ct = default);

    Task<OperationResult<GetFilePathResponse>> GetPath(Guid fileGuid, CancellationToken ct = default);

    Task<OperationResult> Delete(Guid fileGuid, CancellationToken ct = default);
    Task<OperationResult<string>> GetExtension(Guid fileGuid, CancellationToken ct = default);
}
