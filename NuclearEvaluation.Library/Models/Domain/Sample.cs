using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Kernel.Models.Domain;

[Index(nameof(SeriesId))]
public class Sample
{
    [Key]
    public int Id { get; set; }

    public int SeriesId { get; set; }

    public Series Series { get; set; } = null!;

    [StringLength(3, ErrorMessage = "Sample ExternalCode must be a maximum of 3 characters")]
    public string ExternalCode { get; set; } = string.Empty;

    public DateTime SamplingDate { get; set; }

    string _sampleClass = string.Empty;
    public string SampleClass
    {
        get
        {
            return _sampleClass;
        }
        set
        {
            CalculateSampleType(value);
            _sampleClass = value;
        }
    }

    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public SampleType SampleType { get; set; } = SampleType.None;

    void CalculateSampleType(string sampleClass)
    {
        if (sampleClass.StartsWith("PIC", StringComparison.InvariantCultureIgnoreCase))
        {
            SampleType = SampleType.Pic;
        }
        else if (sampleClass.Contains("QC", StringComparison.InvariantCultureIgnoreCase))
        {
            SampleType = SampleType.Qc;
        }
        else
        {
            SampleType = SampleType.Field;
        }
    }

    public static string GetSampleTypeSqlExpression()
    {
        return $"CASE WHEN {nameof(SampleClass)} LIKE 'PIC%' THEN {(byte)SampleType.Pic} WHEN {nameof(SampleClass)} LIKE '%QC%' THEN {(byte)SampleType.Qc} ELSE {(byte)SampleType.Field} END";
    }

    [Precision(11, 8)]
    public decimal? Latitude { get; set; }

    [Precision(11, 8)]
    public decimal? Longitude { get; set; }

    public List<SubSample> SubSamples { get; set; } = [];
}