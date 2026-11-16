using System.Runtime.CompilerServices;
using Kerajel.Primitives.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Server.Interfaces.STEM;
using NuclearEvaluation.Server.Services.Sandbox;
using NuclearEvaluation.Server.Services.STEM;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Tests;

public class UploadCleanupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawWorkbookIsClosedAndOnlySuccessfulImportsAreRetained(bool fail)
    {
        DirectoryInfo storage = Directory.CreateTempSubdirectory("stem-raw-cleanup-");
        try
        {
            Guid sessionId = Guid.NewGuid();
            Guid fileId = Guid.NewGuid();
            IStemPreviewParser parser = Substitute.For<IStemPreviewParser>();
            parser
                .Parse(Arg.Any<Stream>(), "sample.xls", Arg.Any<CancellationToken>())
                .Returns(call => Rows(fail, call.Arg<CancellationToken>()));
            await using StemSessionManager sessions = new(
                Options.Create(new SandboxSettings()),
                storage
            );
            StemPreviewEntryService entries = new(sessions, parser);
            StemPreviewService service = new(entries, NullLogger<StemPreviewService>.Instance);
            using MemoryStream stream = new([17]);
            OperationResult result = await service.UploadStemPreviewFile(
                sessionId,
                stream,
                fileId,
                "sample.xls"
            );

            Assert.Equal(!fail, result.IsSuccessful);
            string[] retainedFiles = Directory.GetFiles(
                storage.FullName,
                "*.xls",
                SearchOption.AllDirectories
            );
            FetchDataResult<StemPreviewEntryView> preview = await entries.GetStemPreviewEntryViews(
                sessionId,
                new()
            );
            if (fail)
            {
                Assert.Empty(retainedFiles);
                Assert.Empty(preview.Entries);
            }
            else
            {
                Assert.Equal(fileId, Assert.Single(preview.Entries).FileId);
                string path = Assert.Single(retainedFiles);
                using FileStream retained = File.Open(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None
                );
                Assert.Equal(17, retained.ReadByte());
            }
        }
        finally
        {
            storage.Delete(recursive: true);
        }
    }

    static async IAsyncEnumerable<StemPreviewEntry> Rows(
        bool fail,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        yield return new StemPreviewEntry { Id = 1, AnalysisDate = new(2026, 1, 1) };
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
        if (fail)
            throw new FormatException("A later row is malformed.");
    }
}
