using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuclearEvaluation.Shared.Models.Domain;

public class ProjectSeries
{
    [Key, Column(Order = 0)]
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public int SeriesId { get; set; }
    public Series Series { get; set; } = null!;
}