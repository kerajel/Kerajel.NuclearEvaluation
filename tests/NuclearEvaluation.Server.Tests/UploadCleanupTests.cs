using System.Runtime.CompilerServices;
using Kerajel.Primitives.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Kernel.Models.Files;
using NuclearEvaluation.Server.Interfaces.EFS;
using NuclearEvaluation.Server.Interfaces.STEM;
using NuclearEvaluation.Server.Services.STEM;

namespace NuclearEvaluation.Server.Tests;

public class UploadCleanupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FilesAreClosedAndRemovedOnSuccessAndParseFailure(bool fail)
    {
        string path = Path.GetTempFileName();
        Guid sessionId = Guid.NewGuid(),
            fileId = Guid.NewGuid();
        IEfsFileService efs = Substitute.For<IEfsFileService>();
        efs.Write(Arg.Any<WriteFileCommand>(), Arg.Any<CancellationToken>())
            .Returns(OperationResult<FileInfo>.Succeeded(new FileInfo(path)));
        efs.Delete(fileId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                File.Delete(path);
                return OperationResult.Succeeded();
            });
        IStemPreviewParser parser = Substitute.For<IStemPreviewParser>();
        parser
            .Parse(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Rows(fail));
        IStemPreviewEntryService entries = Substitute.For<IStemPreviewEntryService>();
        entries
            .InsertStemPreviewEntries(
                sessionId,
                Arg.Any<IAsyncEnumerable<StemPreviewEntry>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(async call =>
            {
                await foreach (
                    StemPreviewEntry row in call.Arg<IAsyncEnumerable<StemPreviewEntry>>()
                )
                    Assert.Equal(fileId, row.FileId);
            });
        try
        {
            StemPreviewService service = new(
                parser,
                entries,
                efs,
                NullLogger<StemPreviewService>.Instance
            );
            using MemoryStream stream = new();
            OperationResult result = await service.UploadStemPreviewFile(
                sessionId,
                stream,
                fileId,
                "sample.csv"
            );
            Assert.Equal(!fail, result.IsSuccessful);
            Assert.False(File.Exists(path));
            await efs.Received(1).Delete(fileId, Arg.Any<CancellationToken>());
            if (fail)
            {
                await entries.Received().DeleteFileData(sessionId, fileId);
                await entries.DidNotReceive().SetStemPreviewFileAsFullyUploaded(sessionId, fileId);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    static async IAsyncEnumerable<StemPreviewEntry> Rows(
        bool fail,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        yield return new StemPreviewEntry { Id = 1 };
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        if (fail)
            throw new FormatException("A later row is malformed.");
    }
}
