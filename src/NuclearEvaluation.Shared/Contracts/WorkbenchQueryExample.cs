namespace NuclearEvaluation.Shared.Contracts;

public static class WorkbenchQueryExample
{
    public const string Title = "California · 2025 R03 coverage review";

    public static WorkbenchQuery Query { get; } =
        new(
            WorkbenchQueryEntity.Series,
            [
                new(
                    WorkbenchQueryEntity.Project,
                    false,
                    [
                        new(
                            nameof(WorkbenchQueryRow.ProjectName),
                            RadNetOperator.Equal,
                            "California monitoring"
                        ),
                    ]
                ),
                new(
                    WorkbenchQueryEntity.Series,
                    false,
                    [new(nameof(WorkbenchQueryRow.State), RadNetOperator.Equal, "CA")]
                ),
                new(
                    WorkbenchQueryEntity.File,
                    false,
                    [new(nameof(WorkbenchQueryRow.Year), RadNetOperator.Equal, "2025")]
                ),
                new(
                    WorkbenchQueryEntity.Observation,
                    false,
                    [
                        new(nameof(WorkbenchQueryRow.Status), RadNetOperator.Equal, "APPROVED"),
                        new(nameof(WorkbenchQueryRow.ExposureMrH), RadNetOperator.IsMissing),
                    ]
                ),
                new(
                    WorkbenchQueryEntity.Channel,
                    false,
                    [
                        new(nameof(WorkbenchQueryRow.Channel), RadNetOperator.Equal, "R03"),
                        new(nameof(WorkbenchQueryRow.CountRate), RadNetOperator.GreaterThan, "100"),
                    ]
                ),
            ],
            nameof(WorkbenchQueryRow.SeriesName),
            MinimumMatchingObservations: 8_000
        );
}
