namespace NuclearEvaluation.Shared.Contracts;

public static class RemDbExample
{
    public const string Title = "Austrian cow milk · Cs-137 below reporting limits";

    public static RemDbQuery Query { get; } =
        new(
            RemDbEntity.Measurement,
            [
                new(
                    RemDbEntity.Site,
                    false,
                    [new(nameof(RemDbRow.Country), RadNetOperator.Equal, "Austria")]
                ),
                new(
                    RemDbEntity.SamplingWindow,
                    false,
                    [new(nameof(RemDbRow.Medium), RadNetOperator.Contains, "MILK COW")]
                ),
                new(
                    RemDbEntity.Measurement,
                    false,
                    [
                        new(nameof(RemDbRow.Nuclide), RadNetOperator.Equal, "CS-137"),
                        new(nameof(RemDbRow.Qualifier), RadNetOperator.Equal, "<"),
                        new(nameof(RemDbRow.Unit), RadNetOperator.Equal, "BQ/M3"),
                        new(nameof(RemDbRow.SourceYear), RadNetOperator.AtLeast, "2012"),
                    ]
                ),
            ],
            nameof(RemDbRow.EndedOn),
            Descending: true
        );
}
