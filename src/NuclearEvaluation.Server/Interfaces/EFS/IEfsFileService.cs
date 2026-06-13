using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.Files;

namespace NuclearEvaluation.Server.Interfaces.EFS;

public interface IEfsFileService
{
    Task<OperationResult<FileInfo>> Write(WriteFileCommand command, CancellationToken ct = default);

    Task<OperationResult<FileInfo>> GetFileInfo(Guid fileGuid, CancellationToken ct = default);

    Task<OperationResult> Delete(Guid fileGuid, CancellationToken ct = default);

    /// <summary>Total bytes currently stored, used to enforce the global storage ceiling.</summary>
    long GetTotalSizeBytes();

    /// <summary>Deletes stored file folders whose last write is older than the cutoff. Returns the count removed.</summary>
    int PurgeOlderThan(DateTime cutoffUtc);
}