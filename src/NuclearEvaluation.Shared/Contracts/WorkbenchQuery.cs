using System.Text.Json.Serialization;

namespace NuclearEvaluation.Shared.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<WorkbenchQueryEntity>))]
public enum WorkbenchQueryEntity
{
    Project,
    Series,
    File,
    Observation,
    Channel,
}

public sealed record WorkbenchQuery(
    WorkbenchQueryEntity Entity = WorkbenchQueryEntity.Observation,
    IReadOnlyList<WorkbenchQueryFilterGroup>? Filters = null,
    string? SortField = null,
    bool Descending = false,
    int? MinimumMatchingObservations = null,
    int Skip = 0,
    int Top = 25,
    IReadOnlyList<WorkbenchQueryCondition>? ResultFilters = null,
    IReadOnlyList<ColumnFilterGroup>? ResultFilterGroups = null
);

public sealed record WorkbenchQueryFilterGroup(
    WorkbenchQueryEntity Entity,
    bool MatchAny,
    IReadOnlyList<WorkbenchQueryCondition> Conditions
);

public sealed record WorkbenchQueryCondition(
    string Field,
    RadNetOperator Operator,
    string? Value = null
);

public sealed record WorkbenchQueryPage(IReadOnlyList<WorkbenchQueryRow> Items, long TotalCount);

public sealed record WorkbenchQueryRow
{
    public Guid? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public int? ProjectSeriesCount { get; init; }
    public Guid? SeriesId { get; init; }
    public string? SeriesName { get; init; }
    public string? State { get; init; }
    public string? Station { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public WorkbenchReview? Review { get; init; }
    public string? Notes { get; init; }
    public long? SeriesObservationCount { get; init; }
    public long? MatchingObservations { get; init; }
    public int? FileId { get; init; }
    public int? Year { get; init; }
    public string? FileName { get; init; }
    public long? ObservationId { get; init; }
    public DateTime? ObservedAt { get; init; }
    public string? Status { get; init; }
    public double? ExposureMrH { get; init; }
    public double? DoseNsvH { get; init; }
    public int? SourceRow { get; init; }
    public string? Channel { get; init; }
    public double? CountRate { get; init; }
}

public sealed record WorkbenchQueryField(
    WorkbenchQueryEntity Entity,
    string Name,
    string Label,
    RadNetFieldType Type,
    bool Nullable = false
);

public static class WorkbenchQueryCatalog
{
    public const int MaxPageSize = RadNetCatalog.MaxPageSize;
    public const int MaxGroups = RadNetCatalog.MaxGroups;
    public const int MaxConditions = RadNetCatalog.MaxConditions;

    public static IReadOnlyList<WorkbenchQueryField> Fields { get; } =
    [
        new(
            WorkbenchQueryEntity.Project,
            nameof(WorkbenchQueryRow.ProjectName),
            "Project name",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Project,
            nameof(WorkbenchQueryRow.ProjectSeriesCount),
            "Linked Series",
            RadNetFieldType.Integer
        ),
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.SeriesName),
            "Series name",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.State),
            "State code",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.Station),
            "Station",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.From),
            "From UTC date",
            RadNetFieldType.DateTime
        ),
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.To),
            "Through UTC date",
            RadNetFieldType.DateTime
        ),
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.Review),
            "Review status",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.Notes),
            "Notes",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.SeriesObservationCount),
            "Observations in Series",
            RadNetFieldType.Integer
        ),
        new(
            WorkbenchQueryEntity.File,
            nameof(WorkbenchQueryRow.Year),
            "Archive year",
            RadNetFieldType.Integer
        ),
        new(
            WorkbenchQueryEntity.File,
            nameof(WorkbenchQueryRow.FileName),
            "File name",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.File,
            nameof(WorkbenchQueryRow.FileId),
            "File ID",
            RadNetFieldType.Integer
        ),
        new(
            WorkbenchQueryEntity.Observation,
            nameof(WorkbenchQueryRow.ObservedAt),
            "Observed at (UTC)",
            RadNetFieldType.DateTime
        ),
        new(
            WorkbenchQueryEntity.Observation,
            nameof(WorkbenchQueryRow.Status),
            "Publication status",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Observation,
            nameof(WorkbenchQueryRow.ExposureMrH),
            "Exposure (mR/h)",
            RadNetFieldType.Number,
            true
        ),
        new(
            WorkbenchQueryEntity.Observation,
            nameof(WorkbenchQueryRow.DoseNsvH),
            "Dose (nSv/h)",
            RadNetFieldType.Number,
            true
        ),
        new(
            WorkbenchQueryEntity.Observation,
            nameof(WorkbenchQueryRow.SourceRow),
            "CSV record number",
            RadNetFieldType.Integer
        ),
        new(
            WorkbenchQueryEntity.Channel,
            nameof(WorkbenchQueryRow.Channel),
            "Gamma channel",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Channel,
            nameof(WorkbenchQueryRow.CountRate),
            "Count rate (CPM)",
            RadNetFieldType.Number
        ),
    ];

    public static IReadOnlyList<WorkbenchQueryField> ResultFields { get; } =
    [
        .. Fields,
        new(
            WorkbenchQueryEntity.Series,
            nameof(WorkbenchQueryRow.MatchingObservations),
            "Matching observations",
            RadNetFieldType.Integer
        ),
        new(
            WorkbenchQueryEntity.File,
            nameof(WorkbenchQueryRow.State),
            "State code",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.File,
            nameof(WorkbenchQueryRow.Station),
            "Station",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Observation,
            nameof(WorkbenchQueryRow.Station),
            "Station",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Observation,
            nameof(WorkbenchQueryRow.FileName),
            "File name",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Channel,
            nameof(WorkbenchQueryRow.ObservedAt),
            "Observed at (UTC)",
            RadNetFieldType.DateTime
        ),
        new(
            WorkbenchQueryEntity.Channel,
            nameof(WorkbenchQueryRow.Station),
            "Station",
            RadNetFieldType.Text
        ),
        new(
            WorkbenchQueryEntity.Channel,
            nameof(WorkbenchQueryRow.SourceRow),
            "CSV record number",
            RadNetFieldType.Integer
        ),
        new(
            WorkbenchQueryEntity.Channel,
            nameof(WorkbenchQueryRow.FileName),
            "File name",
            RadNetFieldType.Text
        ),
    ];

    public static bool CanCompare(WorkbenchQueryField field, RadNetOperator operation) =>
        operation switch
        {
            RadNetOperator.Equal or RadNetOperator.NotEqual => true,
            RadNetOperator.Contains => field.Type == RadNetFieldType.Text,
            RadNetOperator.GreaterThan
            or RadNetOperator.AtLeast
            or RadNetOperator.LessThan
            or RadNetOperator.AtMost => field.Type != RadNetFieldType.Text,
            RadNetOperator.IsPresent or RadNetOperator.IsMissing => field.Nullable,
            _ => false,
        };
}
