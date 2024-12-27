using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NuclearEvaluation.Library.Models.Views;

public class ProjectViewSeriesView
{
    [Key, Column(Order = 0)]
    public int ProjectId { get; set; }
    public ProjectView Project { get; set; } = null!;

    [Key, Column(Order = 1)]
    public int SeriesId { get; set; }
    public SeriesView Series { get; set; } = null!;
}