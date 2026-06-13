using Microsoft.EntityFrameworkCore;

namespace NuclearEvaluation.Kernel.Models.DataManagement.Stem;

/// <summary>
/// Staged STEM preview row. Rows are keyed by the anonymous visitor's preview session
/// and purged by the retention job; they never join the permanent DATA schema.
/// </summary>
public class StemPreviewEntry
{
    public long RowId { get; set; }

    public Guid StemSessionId { get; set; }

    [Precision(10, 2)]
    public decimal Id { get; set; }

    public string LabCode { get; set; } = string.Empty;

    public DateOnly AnalysisDate { get; set; }

    public bool IsNu { get; set; }

    [Precision(38, 15)]
    public decimal? U234 { get; set; }

    [Precision(38, 15)]
    public decimal? ErU234 { get; set; }

    [Precision(38, 15)]
    public decimal? U235 { get; set; }

    [Precision(38, 15)]
    public decimal? ErU235 { get; set; }

    public Guid FileId { get; set; }
}
