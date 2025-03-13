namespace NuclearEvaluation.Kernel.Models.Files;

public record WriteFileCommand(Guid FileId, string FileName, Stream FileContent, bool IsTemporary = false);
