using System.Text.Json.Serialization;

namespace NuclearEvaluation.Shared.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<AssistantPage>))]
public enum AssistantPage
{
    Home,
    Fleet,
    Emissions,
    DataManagement,
    Evaluation,
    Project,
    Unknown,
    FleetGeneration,
    Sources,
    Radiation,
    Radiochemistry,
    Workbench,
}

[JsonConverter(typeof(JsonStringEnumConverter<AssistantSurface>))]
public enum AssistantSurface
{
    None,
    Series,
    Projects,
    Samples,
    SubSamples,
    Apms,
    Particles,
    QueryBuilder,
    StemPreview,
    StemQuality,
    GenerationCalendar,
    WorkbenchSeries,
    WorkbenchProjectObservations,
    WorkbenchQueryResults,
    WorkbenchImportRows,
    WorkbenchArchiveObservations,
}

public sealed record SiteAssistantContext(
    AssistantPage Page,
    AssistantSurface Surface,
    AssistantSurface ResultGrid = AssistantSurface.None,
    bool HasUploads = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        WorkbenchQueryEntity? WorkbenchResultEntity = null
)
{
    [JsonIgnore]
    public AssistantSurface ActiveGrid =>
        Surface switch
        {
            AssistantSurface.QueryBuilder => ResultGrid,
            AssistantSurface.GenerationCalendar => AssistantSurface.None,
            _ => Surface,
        };

    [JsonIgnore]
    public string DisplayName =>
        Surface switch
        {
            AssistantSurface.Series => "Series",
            AssistantSurface.Projects => "Projects",
            AssistantSurface.Samples => "Samples",
            AssistantSurface.SubSamples => "Subsamples",
            AssistantSurface.Apms => "APM measurements",
            AssistantSurface.Particles => "Particles",
            AssistantSurface.QueryBuilder => "Query Builder",
            AssistantSurface.StemPreview => "STEM Preview",
            AssistantSurface.StemQuality => "STEM Quality",
            AssistantSurface.GenerationCalendar => "Generation Calendar",
            AssistantSurface.WorkbenchSeries => "Workbench Series",
            AssistantSurface.WorkbenchProjectObservations => "Project observations",
            AssistantSurface.WorkbenchQueryResults => "Workbench Query Builder results",
            AssistantSurface.WorkbenchImportRows => "Import Review records",
            AssistantSurface.WorkbenchArchiveObservations => "Workbench archive observations",
            _ => Page switch
            {
                AssistantPage.Fleet => "Fleet Explorer",
                AssistantPage.FleetGeneration => "Generation Calendar",
                AssistantPage.Emissions => "Energy & Emissions",
                AssistantPage.Sources => "Sources & Methods",
                AssistantPage.DataManagement => "Data Management",
                AssistantPage.Radiation => "Radiation Monitoring",
                AssistantPage.Radiochemistry => "Radiochemistry",
                AssistantPage.Workbench => "Data Workbench",
                AssistantPage.Evaluation => "Evaluation",
                AssistantPage.Project => "Project overview",
                _ => "Nuclear Evaluation",
            },
        };
}

[JsonConverter(typeof(JsonStringEnumConverter<AssistantActionKind>))]
public enum AssistantActionKind
{
    FilterGrid,
    ClearGrid,
    DraftQuery,
    ShowStemPreview,
    ShowStemQuality,
    CheckStemQuality,
    SelectGenerationView,
    ShowQueryBuilderExample,
    ApplyRadNetQuery,
    ApplyRemDbQuery,
    ShowRemDbExample,
    ApplyWorkbenchGridQuery,
}

public sealed record AssistantGridQuery(
    AssistantSurface Grid,
    IReadOnlyList<SeriesFieldFilter> Filters,
    string? SortField,
    bool Descending,
    int? Limit
)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectSeriesRanking? ProjectSeriesRanking { get; init; }
}

public sealed record ProjectSeriesRanking(
    IReadOnlyList<SeriesFieldFilter> Filters,
    bool Descending
);

public sealed record AssistantBuilderFilter(SeriesQueryEntity Entity, SeriesFieldFilter Filter);

public sealed record AssistantGenerationSelection(
    string? StateCode,
    int? Month,
    bool? DailyAverage
);

public sealed record AssistantAction(AssistantActionKind Kind)
{
    public SeriesGridQuery? SeriesQuery { get; init; }
    public AssistantGridQuery? GridQuery { get; init; }
    public IReadOnlyList<AssistantBuilderFilter>? BuilderFilters { get; init; }
    public StemQualityCheck? QualityCheck { get; init; }
    public AssistantGenerationSelection? GenerationSelection { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RadNetQuery? RadNetQuery { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemDbQuery? RemDbQuery { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssistantWorkbenchGridQuery? WorkbenchGridQuery { get; init; }
}

public sealed record SiteAssistantQuestion(
    string Message,
    SiteAssistantContext Context,
    IReadOnlyList<SiteAssistantTurn>? History = null
);

public sealed record SiteAssistantTurn(string Question, string Reply);

public sealed record SiteAssistantReply(string Message, AssistantAction? Action = null);
