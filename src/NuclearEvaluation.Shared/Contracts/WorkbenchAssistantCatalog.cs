namespace NuclearEvaluation.Shared.Contracts;

public sealed record AssistantWorkbenchGridQuery(
    IReadOnlyList<ColumnFilterGroup> Filters,
    string? SortField = null,
    bool Descending = false,
    int? PageSize = null
);

public sealed record WorkbenchAssistantGridSchema(
    AssistantSurface Surface,
    WorkbenchQueryEntity? ResultEntity,
    IReadOnlyList<WorkbenchAssistantField> Fields
);

public sealed record WorkbenchAssistantField(
    string Name,
    string Label,
    RadNetFieldType Type,
    bool Nullable = false,
    IReadOnlyList<string>? Values = null
);

public static class WorkbenchAssistantCatalog
{
    static readonly string[] ReviewValues = Enum.GetNames<WorkbenchReview>();
    static readonly string[] Dispositions = Enum.GetNames<ImportReviewDisposition>();

    static readonly WorkbenchAssistantField[] ObservationFields = RadNetCatalog
        .Fields.Where(field =>
            field.Name
                is nameof(RadNetRow.ObservedAt)
                    or nameof(RadNetRow.StationName)
                    or nameof(RadNetRow.State)
                    or nameof(RadNetRow.Status)
                    or nameof(RadNetRow.DoseNsvH)
                    or nameof(RadNetRow.ExposureMrH)
                    or nameof(RadNetRow.SourceRow)
        )
        .Select(field => new WorkbenchAssistantField(
            field.Name,
            field.Label,
            field.Type,
            field.Nullable
        ))
        .ToArray();

    public static IReadOnlyList<WorkbenchAssistantGridSchema> Grids { get; } =
    [
        new(
            AssistantSurface.WorkbenchSeries,
            null,
            WorkbenchSeriesColumn
                .All.Select(field => new WorkbenchAssistantField(
                    field.Field,
                    field.Label,
                    field.Type,
                    Values: field.Sort == WorkbenchSeriesSort.Review ? ReviewValues : null
                ))
                .ToArray()
        ),
        new(AssistantSurface.WorkbenchProjectObservations, null, ObservationFields),
        new(AssistantSurface.WorkbenchArchiveObservations, null, ObservationFields),
        new(
            AssistantSurface.WorkbenchImportRows,
            null,
            ImportReviewCatalog
                .Fields.Select(field => new WorkbenchAssistantField(
                    field.Name,
                    field.Label,
                    field.Type,
                    field.Nullable,
                    field.Name == nameof(ImportReviewRow.Disposition) ? Dispositions : null
                ))
                .ToArray()
        ),
        .. WorkbenchQueryCatalog
            .ResultFields.GroupBy(field => field.Entity)
            .Select(result => new WorkbenchAssistantGridSchema(
                AssistantSurface.WorkbenchQueryResults,
                result.Key,
                result
                    .Select(field => new WorkbenchAssistantField(
                        field.Name,
                        field.Label,
                        field.Type,
                        field.Nullable,
                        field.Name == nameof(WorkbenchQueryRow.Review) ? ReviewValues : null
                    ))
                    .ToArray()
            )),
    ];
}
