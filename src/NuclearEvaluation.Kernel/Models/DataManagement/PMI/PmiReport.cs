using LinqToDB.Mapping;
using NuclearEvaluation.Kernel.Models.Identity;

namespace NuclearEvaluation.Kernel.Models.DataManagement.PMI;

public class PmiReport
{
    [PrimaryKey, Identity]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public required string AuthorId { get; set; } = string.Empty;

    public required ApplicationUser Author { get; set; } = null!;

    public DateOnly CreatedDate { get; set; }

    public PmiReportFileMetadata PmiReportFileMetadata { get; set; } = null!;
}