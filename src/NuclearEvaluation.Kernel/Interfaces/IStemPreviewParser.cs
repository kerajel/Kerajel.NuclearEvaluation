using NuclearEvaluation.Kernel.Models.DataManagement.Stem;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IStemPreviewParser
{
    IAsyncEnumerable<StemPreviewEntry> Parse(Stream stream, string fileName, CancellationToken ct = default);
}