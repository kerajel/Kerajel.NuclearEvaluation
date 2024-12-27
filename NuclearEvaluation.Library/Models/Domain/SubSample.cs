using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Library.Models.Domain;

[Index(nameof(SampleId))]
public class SubSample
{
    [Key]
    public int Id { get; set; }

    public int SampleId { get; set; }

    public Sample Sample { get; set; } = null!;

    [StringLength(3, ErrorMessage = "SubSample ExternalCode must be a maximum of 3 characters")]
    public string ExternalCode { get; set; } = string.Empty;

    public DateTime ScreeningDate { get; set; }

    public DateTime? UploadResultDate { get; set; }

    public bool IsFromLegacySystem { get; set; }

    [StringLength(4000)]
    public string ActivityNotes { get; set; } = string.Empty;

    [StringLength(4000)]
    public string TrackingNumber { get; set; } = string.Empty;

    public List<Apm> Apms { get; set; } = [];

    public List<Particle> Particles { get; set; } = [];
}