using LinqToDB;
using LinqToDB.Mapping;

namespace NuclearEvaluation.Kernel.Models.DataManagement.Stem;

/// <summary>A row in a STEM session's temporary preview table.</summary>
[Table(Name = "stem_entries", IsColumnAttributeRequired = false)]
public class StemPreviewEntry
{
    [Column(DataType = DataType.Decimal, Precision = 10, Scale = 2)]
    public decimal Id { get; set; }

    public string LabCode { get; set; } = string.Empty;

    public DateOnly AnalysisDate { get; set; }

    public bool IsNu { get; set; }

    [Column(DataType = DataType.Decimal, Precision = 38, Scale = 15)]
    public decimal? U234 { get; set; }

    [Column(DataType = DataType.Decimal, Precision = 38, Scale = 15)]
    public decimal? ErU234 { get; set; }

    [Column(DataType = DataType.Decimal, Precision = 38, Scale = 15)]
    public decimal? U235 { get; set; }

    [Column(DataType = DataType.Decimal, Precision = 38, Scale = 15)]
    public decimal? ErU235 { get; set; }

    public Guid FileId { get; set; }

    public long RowOrdinal { get; set; }

    public string FileName { get; set; } = string.Empty;
}
