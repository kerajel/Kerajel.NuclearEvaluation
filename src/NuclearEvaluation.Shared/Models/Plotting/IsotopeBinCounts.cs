namespace NuclearEvaluation.Shared.Models.Plotting;

/// <summary>Serializable shape for chart data; the client rebuilds an ILookup from these.</summary>
public class IsotopeBinCounts
{
    public string Isotope { get; set; } = string.Empty;

    public List<BinCount> Bins { get; set; } = [];
}
