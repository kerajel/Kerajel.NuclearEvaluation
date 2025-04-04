using NuclearEvaluation.Kernel.Models.DataManagement.Stem;

namespace NuclearEvaluation.Server.Interfaces.STEM;

public interface IStemPreviewParser
{
    IAsyncEnumerable<StemPreviewEntry> Parse(Stream stream, string fileName, CancellationToken ct = default);
}