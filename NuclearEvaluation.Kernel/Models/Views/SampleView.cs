using NuclearEvaluation.Kernel.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Kernel.Models.Views;

public class SampleView
{
    [Key]
    public int Id { get; set; }

    public int SeriesId { get; set; }

    public SeriesView Series { get; set; } = null!;

    public string ExternalCode { get; set; } = string.Empty;

    public string Sequence { get; set; } = string.Empty;

    public DateTime SamplingDate { get; set; }

    public string SampleClass { get; set; } = string.Empty;

    public SampleType SampleType { get; set; } = SampleType.None;

    [Precision(11, 8)]
    public decimal? Latitude { get; set; }

    [Precision(11, 8)]
    public decimal? Longitude { get; set; }

    public List<SubSampleView> SubSamples { get; set; } = [];

    public int SubSampleCount { get; set; }
}