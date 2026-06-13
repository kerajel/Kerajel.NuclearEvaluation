using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Kernel.Models.Views;

public class PmiReportView
{
    [Key]
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required DateOnly DateUploaded { get; init; }

    public required PmiReportFileMetadataView FileMetadata { get; init; }
}
