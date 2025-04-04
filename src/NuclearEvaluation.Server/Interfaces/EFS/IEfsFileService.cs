using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.Files;

namespace NuclearEvaluation.Server.Interfaces.EFS;

public interface IEfsFileService
{
    Task<OperationResult<FileInfo>> Write(WriteFileCommand command, CancellationToken ct = default);

    Task<OperationResult<FileInfo>> GetFileInfo(Guid fileGuid, CancellationToken ct = default);

    Task<OperationResult> Delete(Guid fileGuid, CancellationToken ct = default);
}