using System.Text.Json.Serialization;

namespace NuclearEvaluation.Shared.Contracts;

public sealed record WorkbenchCatalog(
    RadNetSummary Archive,
    IReadOnlyList<WorkbenchStation> Stations
);

public sealed record WorkbenchStation(
    string State,
    string Name,
    DateOnly FirstDay,
    DateOnly LastDay,
    long ObservationCount,
    long SourceFileCount
);

[JsonConverter(typeof(JsonStringEnumConverter<WorkbenchReview>))]
public enum WorkbenchReview
{
    Pending,
    InReview,
    Reviewed,
}

public sealed record WorkbenchSeries(
    Guid Id,
    string Name,
    string State,
    string Station,
    DateOnly From,
    DateOnly To,
    WorkbenchReview Review,
    string Notes,
    long ObservationCount,
    long Version
);

public sealed record WorkbenchSeriesDraft(
    string Name,
    string State,
    string Station,
    DateOnly From,
    DateOnly To,
    WorkbenchReview Review,
    string Notes,
    long? Version = null
);

[JsonConverter(typeof(JsonStringEnumConverter<WorkbenchSeriesSort>))]
public enum WorkbenchSeriesSort
{
    Name,
    State,
    Station,
    From,
    To,
    Review,
    ObservationCount,
    Notes,
}

public sealed record WorkbenchSeriesSearch(
    string? Search = null,
    string? State = null,
    WorkbenchReview? Review = null,
    WorkbenchSeriesSort Sort = WorkbenchSeriesSort.Name,
    bool Descending = false,
    int Skip = 0,
    int Top = 25,
    IReadOnlyList<ColumnFilterGroup>? ColumnFilters = null
);

public sealed record WorkbenchSeriesColumn(
    WorkbenchSeriesSort Sort,
    string Label,
    RadNetFieldType Type = RadNetFieldType.Text
)
{
    public string Field => Sort.ToString();

    public static IReadOnlyList<WorkbenchSeriesColumn> All { get; } =
    [
        new(WorkbenchSeriesSort.Name, "Series"),
        new(WorkbenchSeriesSort.State, "State"),
        new(WorkbenchSeriesSort.Station, "Station"),
        new(WorkbenchSeriesSort.From, "From UTC", RadNetFieldType.DateTime),
        new(WorkbenchSeriesSort.To, "Through UTC", RadNetFieldType.DateTime),
        new(WorkbenchSeriesSort.Review, "Review"),
        new(WorkbenchSeriesSort.ObservationCount, "Observations", RadNetFieldType.Integer),
        new(WorkbenchSeriesSort.Notes, "Notes"),
    ];
}

public sealed record WorkbenchSeriesPage(
    IReadOnlyList<WorkbenchSeries> Items,
    long TotalCount,
    long ObservationCount
);

public sealed record WorkbenchSeriesFile(int Id, string FileName, int Year, long ObservationCount);

public sealed record WorkbenchFilePage(IReadOnlyList<WorkbenchSeriesFile> Items, long TotalCount);

public sealed record WorkbenchProject(Guid Id, string Name, long Version, int SeriesCount);

public sealed record WorkbenchProjectDraft(string Name);

public sealed record WorkbenchProjectDetails(
    WorkbenchProject Project,
    IReadOnlyList<WorkbenchSeries> Series,
    WorkbenchProjectStats Stats
);

public sealed record WorkbenchProjectStats(
    long ObservationCount,
    long WithExposure,
    long WithR03,
    IReadOnlyList<WorkbenchMonth> Months
);

public sealed record WorkbenchMonth(
    DateOnly Month,
    long ObservationCount,
    long WithExposure,
    long WithR03
);
