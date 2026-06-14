using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Kernel.Data.Queries;

public class PresetFilterQueryObject
{
    public SeriesView? Series { get; set; }
    public SampleView? Sample { get; set; }
    public SubSampleView? SubSample { get; set; }
    public ApmView? Apm { get; set; }
    public ParticleView? Particle { get; set; }
}