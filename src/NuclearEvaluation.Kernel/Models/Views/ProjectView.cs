using NuclearEvaluation.Kernel.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Kernel.Models.Views;

public class ProjectView
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<ProjectViewSeriesView> ProjectSeries { get; set; } = [];

    public string Conclusions { get; set; } = string.Empty;

    public string FollowUpActionsRecommended { get; set; } = string.Empty;

    public DateTime? DecayCorrectionDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string SeriesIds { get; set; } = string.Empty;

    public int SampleCount { get; set; }
}