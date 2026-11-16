namespace NuclearEvaluation.Client.Shared.Fleet;

internal sealed record FleetState(
    string Code,
    int PlantCount,
    decimal? NetSummerCapacityMw,
    decimal? AnnualNetGenerationMwh
)
{
    public string Name => DisplayName(Code);

    public static FleetState[] Summarize(IEnumerable<FleetPlant> plants) =>
        plants
            .GroupBy(plant => plant.State, StringComparer.Ordinal)
            .Select(state => new FleetState(
                state.Key,
                state.Count(),
                FleetSnapshot.SumComplete(state.Select(plant => plant.NetSummerCapacityMw)),
                FleetSnapshot.SumComplete(state.Select(plant => plant.AnnualNetGenerationMwh))
            ))
            .OrderBy(state => state.Name, StringComparer.Ordinal)
            .ToArray();

    public static string DisplayName(string state) =>
        state switch
        {
            "AL" => "Alabama",
            "AR" => "Arkansas",
            "AZ" => "Arizona",
            "CA" => "California",
            "CT" => "Connecticut",
            "FL" => "Florida",
            "GA" => "Georgia",
            "IL" => "Illinois",
            "KS" => "Kansas",
            "LA" => "Louisiana",
            "MD" => "Maryland",
            "MI" => "Michigan",
            "MN" => "Minnesota",
            "MO" => "Missouri",
            "MS" => "Mississippi",
            "NC" => "North Carolina",
            "NE" => "Nebraska",
            "NH" => "New Hampshire",
            "NJ" => "New Jersey",
            "NY" => "New York",
            "OH" => "Ohio",
            "PA" => "Pennsylvania",
            "SC" => "South Carolina",
            "TN" => "Tennessee",
            "TX" => "Texas",
            "VA" => "Virginia",
            "WA" => "Washington",
            "WI" => "Wisconsin",
            _ => state,
        };
}
