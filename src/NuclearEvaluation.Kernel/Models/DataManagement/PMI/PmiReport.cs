using LinqToDB.Mapping;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Models.Identity;

namespace NuclearEvaluation.Kernel.Models.DataManagement.PMI;

public class PmiReport
{
    [PrimaryKey, Identity]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;

    public DateOnly CreatedDate { get; set; }

    public PmiReportStatus Status { get; set; } = PmiReportStatus.Unknown;

    public List<PmiReportDistributionEntry> PmiReportDistributionEntries { get; set; } = [];
}