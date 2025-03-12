using NuclearEvaluation.Kernel.Models.Files;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IFileService
{
    Task Delete(Guid fileGuid, CancellationToken ct);
    Task<ReadFileResponse?> Read(Guid fileGuid, CancellationToken ct);
    Task Write(WriteFileCommand command, CancellationToken ct = default);
}