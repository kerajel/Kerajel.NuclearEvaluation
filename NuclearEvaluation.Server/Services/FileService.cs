using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Files;

namespace NuclearEvaluation.Server.Services
{
    /// <summary>
    /// In an ideal world, our files would be hanging out in S3, but until the budget improves, local storage will have to do!
    /// </summary>
    public class FileService : IFileService
    {
        private const string _subFolder = "NuclearEvaluationStorage";
        private const int _bufferSize = 81920;

        public async Task Write(WriteFileCommand command, CancellationToken ct = default)
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

        public async Task<ReadFileResponse?> Read(Guid fileGuid, CancellationToken ct = default)
        {
            DirectoryInfo fileDirectory = GetFileDirectory(fileGuid);
            if (!fileDirectory.Exists)
            {
                return null;
            }
            FileInfo[] files = fileDirectory.GetFiles();
            if (files.Length == 0)
            {
                return null;
            }
            FileInfo fileInfo = files[0];
            FileStream fileStream = new(
                fileInfo.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                _bufferSize,
                useAsync: true);

            return new ReadFileResponse(fileStream, fileInfo.Name);
        }

        public async Task Delete(Guid fileGuid, CancellationToken ct)
        {
            DirectoryInfo fileDirectory = GetFileDirectory(fileGuid);
            if (fileDirectory.Exists)
            {
                await Task.Run(() =>
                {
                    fileDirectory.Delete(recursive: true);
                }, ct);
            }
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
}