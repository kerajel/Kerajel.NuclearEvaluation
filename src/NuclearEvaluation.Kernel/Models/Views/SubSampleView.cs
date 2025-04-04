using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Kernel.Models.Views;

public class SubSampleView
{
    [Key]
    public int Id { get; set; }

    public int SampleId { get; set; }

    public SampleView Sample { get; set; } = null!;

    public string ExternalCode { get; set; } = string.Empty;

    public string Sequence { get; set; } = string.Empty;

    public DateTime ScreeningDate { get; set; }

    public DateTime? UploadResultDate { get; set; }

    public bool IsFromLegacySystem { get; set; }

    [StringLength(4000)]
    public string ActivityNotes { get; set; } = string.Empty;

    [StringLength(4000)]
    public string TrackingNumber { get; set; } = string.Empty;

    public List<ApmView> Apms { get; set; } = [];

    public List<ParticleView> Particles { get; set; } = [];
}