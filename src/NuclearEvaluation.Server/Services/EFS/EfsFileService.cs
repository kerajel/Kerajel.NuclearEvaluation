using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.Files;
using NuclearEvaluation.Server.Interfaces.EFS;

namespace NuclearEvaluation.Server.Services.EFS;

/// <summary>
/// In an ideal world, our files would be hanging out in Amazon EFS, but until the budget improves, local storage will have to do!
/// </summary>
public class EfsFileService : IEfsFileService
{
    private const string _subFolder = "NuclearEvaluationStorage";
    private const int _bufferSize = 81920;

    public async Task<OperationResult<FileInfo>> Write(WriteFileCommand command, CancellationToken ct = default)
    {
        OperationResult<FileInfo> result;

        try
        {
            DirectoryInfo fileDirectory = GetFileDirectory(command.FileId);
            if (!fileDirectory.Exists)
            {
                fileDirectory.Create();
            }

            FileInfo fileInfo = new(Path.Combine(fileDirectory.FullName, command.FileName));

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
            useAsync: true);

        await stream.CopyToAsync(fileStream, _bufferSize, ct);
    }

    public Task<OperationResult<FileInfo>> GetFileInfo(Guid fileGuid, CancellationToken ct = default)
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
                await Task.Run(() =>
                {
                    fileDirectory.Delete(recursive: true);
                }, ct);
            }
            result = new(OperationStatus.Succeeded);
        }
        catch (Exception ex)
        {
            result = new(OperationStatus.Error, "Could not delete a file", ex);
        }

        return result;
    }
    static DirectoryInfo GetStorageDirectory()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        DirectoryInfo parentDirectoryInfo = Directory.GetParent(currentDirectory)!;
        DirectoryInfo storageDirectory = new(Path.Combine(parentDirectoryInfo.FullName, _subFolder));
        if (!storageDirectory.Exists)
        {
            storageDirectory.Create();
        }
        return storageDirectory;
    }

    static DirectoryInfo GetFileDirectory(Guid fileGuid)
    {
        DirectoryInfo storageDirectory = GetStorageDirectory();
        DirectoryInfo fileDirectory = new(Path.Combine(storageDirectory.FullName, fileGuid.ToString()));
        return fileDirectory;
    }
}