using LinqToDB.Mapping;

namespace NuclearEvaluation.Kernel.Models.DataManagement.PMI;

public class PmiReportFileMetadata
{
    [PrimaryKey, Identity]
    public required Guid Id { get; set; }

    public Guid PmiReportId { get; set; }

    public required PmiReport PmiReport { get; set; }

    public required string FileName { get; set; } = string.Empty;

    public long Size { get; set; }
}