using LinqToDB.Mapping;

namespace NuclearEvaluation.Kernel.Models.Views;

public class PmiReportFileMetadataView
{
    [PrimaryKey, Identity]
    public required Guid Id { get; set; }

    public Guid PmiReportId { get; set; }

    public PmiReportView PmiReport { get; set; } = null!;

    public required string FileName { get; set; } = string.Empty;

    public long Size { get; set; }
}