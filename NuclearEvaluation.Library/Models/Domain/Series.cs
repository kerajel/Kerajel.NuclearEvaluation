using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Library.Models.Domain;

public class Series
{
    [Key]
    public int Id { get; set; }

    public SeriesType SeriesType { get; set; }

    public DateTime CreatedAt { get; set; }

    [StringLength(4000)]
    public string SgasComment { get; set; } = string.Empty;

    [StringLength(200)]
    public string WorkingPaperLink { get; set; } = string.Empty;

    public bool IsDu { get; set; }

    public bool IsNu { get; set; }

    public DateTime? AnalysisCompleteDate { get; set; }

    public List<Sample> Samples { get; set; } = [];

    public List<ProjectSeries> ProjectSeries { get; set; } = [];
}