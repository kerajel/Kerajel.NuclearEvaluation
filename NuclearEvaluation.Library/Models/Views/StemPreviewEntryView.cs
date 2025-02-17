using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Library.Models.Views;

public class StemPreviewEntryView
{
    [Key]
    public decimal Id { get; set; }
    public string LabCode { get; set; } = string.Empty;
    public DateOnly AnalysisDate { get; set; }
    public bool IsNu { get; set; }
    public decimal? U234 { get; set; }
    public decimal? ErU234 { get; set; }
    public decimal? U235 { get; set; }
    public decimal? ErU235 { get; set; }
}