using System.Globalization;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Radiation;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Pages;

public partial class Radiation : IDisposable
{
    static readonly (string View, string Name, string Icon)[] Views =
    [
        ("stations", "Browse", "monitoring"),
        ("builder", "Query Builder", "tune"),
        ("quality", "File quality", "fact_check"),
    ];

    static readonly RadNetQuery StationQuery = new(RadNetEntity.Station);
    readonly CancellationTokenSource _lifetime = new();
    readonly RadNetQuery _qualityQuery = new(RadNetEntity.SourceFile);
    readonly List<BrowseStep> _path = [new("All stations", StationQuery)];
    RadNetQuery _browseQuery = StationQuery;
    RadNetSummary? _summary;
    RadNetBuilder? _builder;
    PendingAssistantQuery? _pendingAssistantQuery;
    string? _summaryError;
    string _stationSearch = "";
    bool _loadingSummary = true;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    NavigationManager Navigation { get; set; } = null!;

    [Inject]
    SiteAssistantWorkspace AssistantWorkspace { get; set; } = null!;

    [SupplyParameterFromQuery(Name = "view")]
    public string? View { get; set; }

    [SupplyParameterFromQuery(Name = "file")]
    public string? RequestedFile { get; set; }

    string ActiveView => View is "builder" or "quality" ? View : "stations";
    int? FileId =>
        int.TryParse(RequestedFile, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
        && id > 0
            ? id
            : null;

    protected override Task OnInitializedAsync() => LoadDataAsync();

    protected override void OnParametersSet() => PublishAssistantContext();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (
            _pendingAssistantQuery is not { } pending
            || ActiveView != "builder"
            || _builder is null
        )
            return;

        _pendingAssistantQuery = null;
        await _builder.ApplyAssistantQueryAsync(pending.Query);
        pending.Completion.TrySetResult(true);
    }

    async Task LoadDataAsync()
    {
        _loadingSummary = true;
        _summaryError = null;
        try
        {
            _summary = await Http.GetFromJsonAsync<RadNetSummary>(
                "api/radnet/summary",
                _lifetime.Token
            );
        }
        catch (Exception error)
            when (error is HttpRequestException or System.Text.Json.JsonException)
        {
            _summary = null;
            _summaryError = "Could not contact the archive. Try again shortly.";
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        finally
        {
            _loadingSummary = false;
            PublishAssistantContext();
        }
    }

    void SearchStations()
    {
        string text = _stationSearch.Trim();
        _browseQuery =
            text.Length == 0
                ? StationQuery
                : StationQuery with
                {
                    Filters =
                    [
                        new(
                            RadNetEntity.Station,
                            true,
                            [
                                new(nameof(RadNetRow.StationName), RadNetOperator.Contains, text),
                                new(nameof(RadNetRow.State), RadNetOperator.Equal, text),
                            ]
                        ),
                    ],
                };
        _path[0] = new("All stations", _browseQuery);
        PublishAssistantContext();
    }

    void DrillDown(RadNetRow row)
    {
        BrowseStep? step = _browseQuery.Entity switch
        {
            RadNetEntity.Station => StationStep(row),
            RadNetEntity.SourceFile => FileStep(row),
            RadNetEntity.Observation => ObservationStep(row),
            _ => null,
        };
        if (step is null)
            return;

        _path.Add(step);
        _browseQuery = step.Query;
        PublishAssistantContext();
    }

    static BrowseStep StationStep(RadNetRow row) =>
        new(
            row.StationName,
            new(
                RadNetEntity.SourceFile,
                [ById(RadNetEntity.Station, nameof(RadNetRow.StationId), row.StationId)]
            )
        );

    static BrowseStep? FileStep(RadNetRow row) =>
        row.SourceFileId is int id
            ? new(
                row.FileName ?? row.Year?.ToString() ?? "Source file",
                new(
                    RadNetEntity.Observation,
                    [ById(RadNetEntity.SourceFile, nameof(RadNetRow.SourceFileId), id)]
                )
            )
            : null;

    static BrowseStep? ObservationStep(RadNetRow row) =>
        row.ObservationId is long id
            ? new(
                RadNetPresentation.Timestamp(row.ObservedAt),
                new(
                    RadNetEntity.Channel,
                    [ById(RadNetEntity.Observation, nameof(RadNetRow.ObservationId), id)]
                )
            )
            : null;

    static RadNetFilterGroup ById(RadNetEntity entity, string field, long id) =>
        new(
            entity,
            false,
            [new(field, RadNetOperator.Equal, FormattableString.Invariant($"{id}"))]
        );

    void OpenStep(BrowseStep step)
    {
        int index = _path.IndexOf(step);
        if (index < 0)
            return;

        _path.RemoveRange(index + 1, _path.Count - index - 1);
        _browseQuery = step.Query;
        PublishAssistantContext();
    }

    void OpenQuality(RadNetRow row)
    {
        if (row.SourceFileId is int id)
            Navigation.NavigateTo(FormattableString.Invariant($"radiation?view=quality&file={id}"));
    }

    void BrowseQualityFile(RadNetRow row)
    {
        if (FileStep(row) is not { } step)
            return;

        _path.RemoveRange(1, _path.Count - 1);
        _path.Add(StationStep(row));
        _path.Add(step);
        _browseQuery = step.Query;
        Navigation.NavigateTo("radiation");
    }

    void BrowseQueryResult((RadNetEntity Entity, RadNetRow Row) selection)
    {
        (RadNetEntity entity, RadNetRow row) = selection;
        BrowseStep? observation = entity == RadNetEntity.Observation ? ObservationStep(row) : null;
        BrowseStep? file = entity is RadNetEntity.SourceFile or RadNetEntity.Observation
            ? FileStep(row)
            : null;

        _path.RemoveRange(1, _path.Count - 1);
        _path.Add(StationStep(row));
        if (file is not null)
            _path.Add(file);
        if (observation is not null)
            _path.Add(observation);

        _browseQuery = _path[^1].Query;
        Navigation.NavigateTo("radiation");
    }

    (string View, int? File, RadNetQuery? Browse) AssistantScope =>
        (ActiveView, FileId, ActiveView == "stations" ? _browseQuery : null);

    void PublishAssistantContext()
    {
        if (_lifetime.IsCancellationRequested)
            return;
        if (_summary is not { Available: true })
        {
            AssistantWorkspace.Remove(this);
            return;
        }

        (string View, int? File, RadNetQuery? Browse) scope = AssistantScope;
        AssistantWorkspace.Set(
            this,
            new(AssistantPage.Radiation, AssistantSurface.None),
            action =>
                AssistantScope == scope
                    ? ApplyAssistantActionAsync(action)
                    : Task.FromResult(false),
            scope
        );
    }

    async Task<bool> ApplyAssistantActionAsync(AssistantAction action)
    {
        if (
            _lifetime.IsCancellationRequested
            || _summary is not { Available: true }
            || action is not { Kind: AssistantActionKind.ApplyRadNetQuery, RadNetQuery: { } query }
        )
            return false;
        if (ActiveView == "builder" && _builder is { } builder)
        {
            await builder.ApplyAssistantQueryAsync(query);
            return true;
        }

        TaskCompletionSource<bool> completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pendingAssistantQuery?.Completion.TrySetResult(false);
        _pendingAssistantQuery = new(query, completion);
        Navigation.NavigateTo(ViewUrl("builder"));
        return await completion.Task;
    }

    static string ViewUrl(string view) =>
        view == "stations" ? "radiation" : $"radiation?view={view}";

    static string EntityName(RadNetEntity entity) => RadNetPresentation.EntityName(entity);

    static string Count(long count) => RadNetPresentation.Count(count);

    static string Coverage(RadNetSummary summary)
    {
        if (summary.Coverage.Count == 0)
            return "—";

        int first = summary.Coverage.Min(item => item.Year);
        int last = summary.Coverage.Max(item => item.Year);
        return first == last ? $"{first}" : $"{first}–{last}";
    }

    public void Dispose()
    {
        _pendingAssistantQuery?.Completion.TrySetResult(false);
        AssistantWorkspace.Remove(this);
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    sealed record BrowseStep(string Label, RadNetQuery Query);

    sealed record PendingAssistantQuery(RadNetQuery Query, TaskCompletionSource<bool> Completion);
}
