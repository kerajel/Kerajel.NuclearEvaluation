namespace NuclearEvaluation.Client.Shared.Fleet;

public sealed record FleetSnapshot(int Year, FleetPlant[] Plants)
{
    public const string AssetPath = "data/fleet-2024.json";
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
