using System.Text.Json.Serialization;

namespace NuclearEvaluation.Client.Shared.Fleet;

public sealed record FleetSnapshot(int Year, FleetPlant[] Plants)
{
    public const string AssetPath = "data/fleet-2024.json";

    [JsonIgnore]
    public int UnitCount => Plants.Sum(plant => plant.Units.Length);

    [JsonIgnore]
    public decimal? NetSummerCapacityMw =>
        SumComplete(Plants.Select(plant => plant.NetSummerCapacityMw));

    [JsonIgnore]
    public decimal? AnnualNetGenerationMwh =>
        SumComplete(Plants.Select(plant => plant.AnnualNetGenerationMwh));

    internal static decimal? SumComplete(IEnumerable<decimal?> values)
    {
        decimal total = 0;
        foreach (decimal? value in values)
        {
            if (value is not decimal reported)
                return null;
            total += reported;
        }

        return total;
    }
}

public sealed record FleetPlant(
    int Id,
    string Name,
    string State,
    double? Latitude,
    double? Longitude,
    FleetUnit[] Units,
    decimal?[] MonthlyNetGenerationMwh
)
{
    public decimal? NetSummerCapacityMw =>
        Units.Length > 0 && Units.All(unit => unit.NetSummerCapacityMw is not null)
            ? Units.Sum(unit => unit.NetSummerCapacityMw)
            : null;

    public int ReportedMonths => MonthlyNetGenerationMwh.Count(value => value.HasValue);

    public decimal? AnnualNetGenerationMwh =>
        ReportedMonths == 12 ? MonthlyNetGenerationMwh.Sum() : null;
}

public sealed record FleetUnit(string Id, decimal? NetSummerCapacityMw);
