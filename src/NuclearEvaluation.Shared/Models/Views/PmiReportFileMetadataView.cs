using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Shared.Models.Views;

public class PmiReportFileMetadataView
{
    [Key]
    public required Guid Id { get; set; }

    public Guid PmiReportId { get; set; }

    public PmiReportView PmiReport { get; set; } = null!;

    public required string FileName { get; set; } = string.Empty;

    public long Size { get; set; }
}
