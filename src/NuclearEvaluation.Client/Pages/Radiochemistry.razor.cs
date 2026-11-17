using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Radiochemistry;
using NuclearEvaluation.Shared.Contracts;
using static NuclearEvaluation.Client.Shared.Radiochemistry.RemDbPresentation;

namespace NuclearEvaluation.Client.Pages;

public partial class Radiochemistry : IDisposable
{
    static readonly (string View, string Name, string Icon)[] Views =
    [
        ("browse", "Browse", "travel_explore"),
        ("builder", "Query Builder", "tune"),
        ("quality", "File quality", "fact_check"),
    ];

    static readonly RemDbQuery SiteQuery = new(RemDbEntity.Site);
    readonly List<BrowseStep> _path = [new("All sites", SiteQuery)];
    readonly CancellationTokenSource _lifetime = new();
    RemDbSummary? _summary;
    RemDbBuilder? _builder;
    PendingAssistantQuery? _pendingAssistantQuery;
    string? _summaryError;
    string _country = "";
    string _siteSearch = "";
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

    [SupplyParameterFromQuery(Name = "example")]
    public string? Example { get; set; }

    RemDbQuery BrowseQuery => _path[^1].Query;
    string ActiveView => View is "builder" or "quality" ? View : "browse";
    int? FileId =>
        int.TryParse(RequestedFile, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
        && id > 0
            ? id
            : null;
    bool ExampleRequested => bool.TryParse(Example, out bool requested) && requested;
    string BrowseDescription =>
        BrowseQuery.Entity switch
        {
            RemDbEntity.Site => "Choose a site to inspect its sampling history.",
            RemDbEntity.SamplingWindow =>
                "A window groups records by site, medium, treatment, and published sampling dates and times.",
            _ =>
                "Activity, units, and qualifiers are shown as published. Follow the CSV filename for provenance.",
        };

    protected override Task OnInitializedAsync() => LoadSummaryAsync();

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

    async Task LoadSummaryAsync()
    {
        _loadingSummary = true;
        _summaryError = null;
        try
        {
            _summary = await Http.GetFromJsonAsync<RemDbSummary>(
                "api/remdb/summary",
                _lifetime.Token
            );
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            _summary = null;
            _summaryError = "The archive could not be reached. Try again shortly.";
        }
        finally
        {
            _loadingSummary = false;
            PublishAssistantContext();
        }
    }

    void SearchSites()
    {
        List<RemDbCondition> conditions = [];
        if (_country.Length != 0)
            conditions.Add(new(nameof(RemDbRow.Country), RadNetOperator.Equal, _country));
        if (_siteSearch.Trim() is { Length: > 0 } name)
            conditions.Add(new(nameof(RemDbRow.SiteName), RadNetOperator.Contains, name));

        _path[0] = new(
            _country.Length == 0 ? "All sites" : _country,
            SiteQuery with
            {
                Filters = conditions.Count == 0 ? null : [new(RemDbEntity.Site, false, conditions)],
            }
        );
        PublishAssistantContext();
    }

    void DrillDown(RemDbRow row) => AddDrillDown(BrowseQuery.Entity, row);

    void AddDrillDown(RemDbEntity entity, RemDbRow row)
    {
        if (entity == RemDbEntity.Site)
            _path.Add(SiteStep(row));
        else if (entity == RemDbEntity.SamplingWindow && row.WindowId is long id)
            _path.Add(
                new(
                    $"{Display(row.Medium)} · {DateRange(row)}",
                    QueryBy(
                        RemDbEntity.Measurement,
                        RemDbEntity.SamplingWindow,
                        nameof(RemDbRow.WindowId),
                        id
                    )
                )
            );
        PublishAssistantContext();
    }

    static BrowseStep SiteStep(RemDbRow row) =>
        new(
            SiteLabel(row),
            QueryBy(
                RemDbEntity.SamplingWindow,
                RemDbEntity.Site,
                nameof(RemDbRow.SiteId),
                row.SiteId
            )
        );

    void BrowseQueryResult((RemDbEntity Entity, RemDbRow Row) selection)
    {
        ResetToSites();
        if (selection.Entity == RemDbEntity.SamplingWindow)
            _path.Add(SiteStep(selection.Row));
        AddDrillDown(selection.Entity, selection.Row);
        Navigation.NavigateTo(ViewUrl("browse"));
    }

    void BrowseFile(RemDbSourceFile file)
    {
        ResetToSites();
        _path.Add(
            new(
                file.FileName,
                QueryBy(
                    RemDbEntity.Measurement,
                    RemDbEntity.Measurement,
                    nameof(RemDbRow.SourceFileId),
                    file.Id
                )
            )
        );
        Navigation.NavigateTo(ViewUrl("browse"));
    }

    void BrowseSelectedYear(ChangeEventArgs args)
    {
        if (
            !int.TryParse(
                args.Value?.ToString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int year
            )
        )
            return;

        ResetToSites();
        _path.Add(
            new(
                FormattableString.Invariant($"Source year {year}"),
                QueryBy(
                    RemDbEntity.Measurement,
                    RemDbEntity.Measurement,
                    nameof(RemDbRow.SourceYear),
                    year
                )
            )
        );
        PublishAssistantContext();
    }

    void ResetToSites()
    {
        _country = "";
        _siteSearch = "";
        _path[0] = new("All sites", SiteQuery);
        _path.RemoveRange(1, _path.Count - 1);
    }

    void OpenStep(BrowseStep step)
    {
        int index = _path.IndexOf(step);
        if (index < 0)
            return;

        _path.RemoveRange(index + 1, _path.Count - index - 1);
        PublishAssistantContext();
    }

    static RemDbQuery QueryBy(RemDbEntity result, RemDbEntity group, string field, long value) =>
        new(
            result,
            [
                new(
                    group,
                    false,
                    [new(field, RadNetOperator.Equal, value.ToString(CultureInfo.InvariantCulture))]
                ),
            ]
        );

    static string ViewUrl(string view) => view == "browse" ? PagePath : $"{PagePath}?view={view}";

    (string View, bool Example, int? File, RemDbQuery? Browse) AssistantScope =>
        (ActiveView, ExampleRequested, FileId, ActiveView == "browse" ? BrowseQuery : null);

    void PublishAssistantContext()
    {
        if (_lifetime.IsCancellationRequested)
            return;
        if (_summary is not { Available: true })
        {
            AssistantWorkspace.Remove(this);
            return;
        }

        (string View, bool Example, int? File, RemDbQuery? Browse) scope = AssistantScope;
        AssistantWorkspace.Set(
            this,
            new(AssistantPage.Radiochemistry, AssistantSurface.None),
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
            || action is not { Kind: AssistantActionKind.ApplyRemDbQuery, RemDbQuery: { } query }
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

    public void Dispose()
    {
        _pendingAssistantQuery?.Completion.TrySetResult(false);
        AssistantWorkspace.Remove(this);
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    sealed record BrowseStep(string Label, RemDbQuery Query);

    sealed record PendingAssistantQuery(RemDbQuery Query, TaskCompletionSource<bool> Completion);
}
