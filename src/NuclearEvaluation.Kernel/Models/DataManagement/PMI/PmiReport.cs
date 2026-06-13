using LinqToDB.Mapping;

namespace NuclearEvaluation.Kernel.Models.DataManagement.PMI;

public class PmiReport
{
    [PrimaryKey, Identity]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly CreatedDate { get; set; }

    public PmiReportFileMetadata PmiReportFileMetadata { get; set; } = null!;
}