using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Models.Files;
using NuclearEvaluation.Server.Services.Files;
using NuclearEvaluation.Server.Services.Sandbox;

namespace NuclearEvaluation.Server.Services.EFS;

/// <summary>
/// In an ideal world, our files would be hanging out in Amazon EFS, but until the budget improves, local storage will have to do!
/// </summary>
public class EfsFileService(IOptions<SandboxSettings> settings, IHostEnvironment environment)
    : IEfsFileService
{
    private const int _bufferSize = 81920;
    readonly string _storagePath = settings
        .Value.ResolveStorageDirectory(environment.ContentRootPath)
        .FullName;

    public async Task<OperationResult<FileInfo>> Write(
        WriteFileCommand command,
        CancellationToken ct = default
    )
    {
        OperationResult<FileInfo> result;

        try
        {
            DirectoryInfo fileDirectory = GetFileDirectory(command.FileId);
            if (!fileDirectory.Exists)
            {
                fileDirectory.Create();
            }

            string safeFileName = SafeFileName.FromClientFileName(command.FileName, command.FileId);
            FileInfo fileInfo = new(Path.Combine(fileDirectory.FullName, safeFileName));

            await WriteToFile(fileInfo, command.FileContent, ct);

            if (command.IsTemporary)
            {
                fileInfo.Attributes = FileAttributes.Temporary;
            }

            result = new(OperationStatus.Succeeded, fileInfo);
        }
        catch (Exception ex)
        {
            result = new(OperationStatus.Error, "Could not write file", ex);
        }

        return result;
    }

    static async Task WriteToFile(FileInfo fileInfo, Stream stream, CancellationToken ct = default)
    {
        using FileStream fileStream = new(
            fileInfo.FullName,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            _bufferSize,
            useAsync: true
        );

        await stream.CopyToAsync(fileStream, _bufferSize, ct);
    }

    public Task<OperationResult<FileInfo>> GetFileInfo(
        Guid fileGuid,
        CancellationToken ct = default
    )
    {
        OperationResult<FileInfo> result;

        try
        {
            DirectoryInfo fileDirectory = GetFileDirectory(fileGuid);
            if (!fileDirectory.Exists || fileDirectory.GetFiles().Length == 0)
            {
                result = new(OperationStatus.NotFound, "File not found");
                return Task.FromResult(result);
            }

            FileInfo fileInfo = fileDirectory.GetFiles()[0];
            result = new(OperationStatus.Succeeded, fileInfo);
        }
        catch (Exception ex)
        {
            result = new(OperationStatus.Error, "Could not access file", ex);
        }

        return Task.FromResult(result);
    }

    public async Task<OperationResult> Delete(Guid fileGuid, CancellationToken ct = default)
    {
        OperationResult result;

        try
        {
            DirectoryInfo fileDirectory = GetFileDirectory(fileGuid);
            if (fileDirectory.Exists)
            {
                await Task.Run(
                    () =>
                    {
                        fileDirectory.Delete(recursive: true);
                    },
                    ct
                );
            }
            result = new(OperationStatus.Succeeded);
        }
        catch (Exception ex)
        {
            result = new(OperationStatus.Error, "Could not delete a file", ex);
        }

        return result;
    }

    public long GetTotalSizeBytes()
    {
        DirectoryInfo storageDirectory = GetStorageDirectory();
        return storageDirectory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
    }

    public int PurgeOlderThan(DateTime cutoffUtc, string? excludedDirectory = null) =>
        PurgeOlderThan(GetStorageDirectory(), cutoffUtc, excludedDirectory);

    internal static int PurgeOlderThan(
        DirectoryInfo storageDirectory,
        DateTime cutoffUtc,
        string? excludedDirectory
    )
    {
        int removed = 0;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (DirectoryInfo fileDirectory in storageDirectory.EnumerateDirectories())
        {
            if (string.Equals(fileDirectory.FullName, excludedDirectory, comparison))
                continue;

            if (fileDirectory.LastWriteTimeUtc < cutoffUtc)
            {
                try
                {
                    fileDirectory.Delete(recursive: true);
                    removed++;
                }
                catch
                {
                    // best-effort purge; a locked folder will be retried next sweep
                }
            }
        }

        return removed;
    }

    DirectoryInfo GetStorageDirectory() => Directory.CreateDirectory(_storagePath);

    DirectoryInfo GetFileDirectory(Guid fileGuid)
    {
        DirectoryInfo storageDirectory = GetStorageDirectory();
        DirectoryInfo fileDirectory = new(
            Path.Combine(storageDirectory.FullName, fileGuid.ToString())
        );
        return fileDirectory;
    }
}
