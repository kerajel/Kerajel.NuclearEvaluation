using NuclearEvaluation.Kernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Kernel.Models.Views;

public abstract class ParticleBase
{
    [Key]
    public int Id { get; set; }

    public int SubSampleId { get; set; }
    public SubSampleView SubSample { get; set; } = null!;

    [Precision(10, 2)]
    public decimal ParticleExternalId { get; set; }

    public DateTime AnalysisDate { get; set; }

    public bool IsNu { get; set; }

    [StringLength(15)]
    public string LaboratoryCode { get; set; } = string.Empty;

    [Precision(38, 15)]
    public decimal? U234 { get; set; }

    [Precision(38, 15)]
    public decimal? ErU234 { get; set; }

    [Precision(38, 15)]
    public decimal? U235 { get; set; }

    [Precision(38, 15)]
    public decimal? ErU235 { get; set; }

    [StringLength(4000)]
    public string Comment { get; set; } = string.Empty;
}

public class ParticleView : ParticleBase
{

}

public class ProjectDecayCorrectedParticleView : ParticleView
{
    public int ProjectId { get; set; }
}