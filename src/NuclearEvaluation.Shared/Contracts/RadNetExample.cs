namespace NuclearEvaluation.Shared.Contracts;

public static class RadNetExample
{
    public const string Title = "California approved observations with R03 above 100 CPM";

    public static RadNetQuery Query { get; } =
        new(
            RadNetEntity.Observation,
            [
                new(
                    RadNetEntity.Station,
                    false,
                    [new(nameof(RadNetRow.State), RadNetOperator.Equal, "CA")]
                ),
                new(
                    RadNetEntity.Observation,
                    false,
                    [new(nameof(RadNetRow.Status), RadNetOperator.Equal, "APPROVED")]
                ),
                new(
                    RadNetEntity.Channel,
                    false,
                    [
                        new(nameof(RadNetRow.Channel), RadNetOperator.Equal, "R03"),
                        new(nameof(RadNetRow.CountRate), RadNetOperator.GreaterThan, "100"),
                    ]
                ),
            ],
            nameof(RadNetRow.ObservedAt),
            Descending: true
        );
}
