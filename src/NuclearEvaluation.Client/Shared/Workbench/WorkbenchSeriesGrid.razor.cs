using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Workbench;

public partial class WorkbenchSeriesGrid : IDisposable
{
    static readonly WorkbenchReview[] Reviews = Enum.GetValues<WorkbenchReview>();
    static readonly (string Value, string Label)[] ReviewValues = Reviews
        .Select(review => (review.ToString(), ReviewLabel(review)))
        .ToArray();

    readonly CancellationTokenSource _lifetime = new();
    readonly HashSet<Guid> _expanded = [];
    CancellationTokenSource? _activeLoad;
    WorkbenchSeriesPage? _page;
    WorkbenchSeriesColumn? _filterColumn;
    GridQuery _requestedQuery = new();
    GridQuery _displayedQuery = new();
    SeriesEditor? _draft;
    Guid? _deleting;
    string _search = "";
    string _state = "";
    string _review = "";
    string? _error;
    string? _notice;
    int _pageSize = 20;
    bool _loadFailed;
    bool _loading = true;
    bool _busy;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    SiteAssistantWorkspace AssistantWorkspace { get; set; } = null!;

    [Parameter, EditorRequired]
    public IReadOnlyList<WorkbenchStation> Stations { get; set; } = [];

    IEnumerable<string> States => Stations.Select(station => station.State).Distinct().Order();
    GridQuery ActiveQuery => _loading ? _requestedQuery : _displayedQuery;
    bool HasFilters =>
        _displayedQuery.Search.Length > 0
        || _displayedQuery.State.Length > 0
        || _displayedQuery.Review.Length > 0;

    protected override Task OnInitializedAsync() => ReloadAsync();

    protected override void OnAfterRender(bool firstRender)
    {
        if (_page is not null)
            AssistantWorkspace.Set(
                this,
                new(AssistantPage.Workbench, AssistantSurface.WorkbenchSeries),
                ApplyAssistantActionAsync
            );
    }

    async Task<bool> ApplyAssistantActionAsync(AssistantAction action)
    {
        bool applied = false;
        await InvokeAsync(async () =>
        {
            if (
                _lifetime.IsCancellationRequested
                || !ReferenceEquals(AssistantWorkspace.Current?.Owner, this)
                || _page is null
                || _loading
                || _busy
                || _draft is not null
                || _deleting is not null
            )
                return;

            GridQuery next = _displayedQuery;
            if (action.Kind == AssistantActionKind.ClearGrid)
            {
                _filterColumn = null;
                next = next with { ColumnFilters = null };
            }
            else if (
                action is
                { Kind: AssistantActionKind.ApplyWorkbenchGridQuery, WorkbenchGridQuery: { } query }
            )
            {
                if (query.SortField is { } field)
                {
                    WorkbenchSeriesColumn? column = WorkbenchSeriesColumn.All.FirstOrDefault(
                        column =>
                            string.Equals(column.Field, field, StringComparison.OrdinalIgnoreCase)
                    );
                    if (column is null)
                        return;
                    next = next with { Sort = column.Sort, Descending = query.Descending };
                }
                next = WithColumnFilters(next, query.Filters) with
                {
                    Top = query.PageSize ?? next.Top,
                };
            }
            else
            {
                return;
            }

            Task<bool> load = LoadAsync(next with { Skip = 0 });
            StateHasChanged();
            applied = await load;
            if (!_lifetime.IsCancellationRequested)
                StateHasChanged();
        });
        return applied;
    }

    Task ApplyFiltersAsync() =>
        LoadAsync(
            ActiveQuery with
            {
                Search = _search.Trim(),
                State = _state,
                Review = _review,
                Skip = 0,
            }
        );

    Task ResetFiltersAsync()
    {
        _search = _state = _review = "";
        return ApplyFiltersAsync();
    }

    Task SortAsync(WorkbenchSeriesSort sort)
    {
        GridQuery query = ActiveQuery;
        return LoadAsync(
            query with
            {
                Sort = sort,
                Descending = sort == query.Sort && !query.Descending,
                Skip = 0,
            }
        );
    }

    string? SortDirection(WorkbenchSeriesSort sort) =>
        sort != _displayedQuery.Sort ? null
        : _displayedQuery.Descending ? "descending"
        : "ascending";

    ColumnFilterGroup? FindColumnFilter(string field) =>
        _displayedQuery.ColumnFilters?.FirstOrDefault(group =>
            string.Equals(group.Field, field, StringComparison.OrdinalIgnoreCase)
        );

    void OpenColumnFilter(WorkbenchSeriesColumn column)
    {
        if (!_loading && !_busy && _draft is null)
            _filterColumn = _filterColumn == column ? null : column;
    }

    void CloseColumnFilter() => _filterColumn = null;

    Task ApplyColumnFilterAsync(ColumnFilterGroup filter) =>
        LoadAsync(WithColumnFilters(_displayedQuery, [filter]) with { Skip = 0 });

    static GridQuery WithColumnFilters(GridQuery query, IReadOnlyList<ColumnFilterGroup> filters)
    {
        if (filters.Count == 0)
            return query;

        HashSet<string> fields = filters
            .Select(filter => filter.Field)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return query with
        {
            ColumnFilters =
            [
                .. (query.ColumnFilters ?? []).Where(group => !fields.Contains(group.Field)),
                .. filters,
            ],
        };
    }

    Task ClearColumnFiltersAsync(string? field = null)
    {
        IReadOnlyList<ColumnFilterGroup>? groups = field is null
            ? null
            : _displayedQuery
                .ColumnFilters?.Where(group =>
                    !string.Equals(group.Field, field, StringComparison.OrdinalIgnoreCase)
                )
                .ToArray();
        return LoadAsync(_displayedQuery with { ColumnFilters = groups, Skip = 0 });
    }

    Task ChangePageSizeAsync() => LoadAsync(ActiveQuery with { Skip = 0, Top = _pageSize });

    Task PageAsync(int direction)
    {
        if (_loading)
            return Task.CompletedTask;
        int skip = Math.Max(0, _displayedQuery.Skip + direction * _displayedQuery.Top);
        return LoadAsync(_displayedQuery with { Skip = skip });
    }

    Task ReloadAsync() => LoadAsync(_displayedQuery);

    Task RetryAsync() => LoadAsync(_requestedQuery);

    async Task<bool> LoadAsync(GridQuery query)
    {
        if (_lifetime.IsCancellationRequested)
            return false;

        _activeLoad?.Cancel();
        using CancellationTokenSource load = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetime.Token
        );
        _activeLoad = load;
        _requestedQuery = query;
        _loading = true;

        try
        {
            WorkbenchSeriesPage page = await ReadPageAsync(query, load.Token);
            if (!ReferenceEquals(load, _activeLoad))
                return false;

            if (query.Skip > 0 && query.Skip >= page.TotalCount)
            {
                query = query with
                {
                    Skip = (int)(Math.Max(0, page.TotalCount - 1) / query.Top * query.Top),
                };
                page = await ReadPageAsync(query, load.Token);
            }

            if (ReferenceEquals(load, _activeLoad))
            {
                if (query != _displayedQuery)
                {
                    _notice = null;
                    _deleting = null;
                }
                _expanded.IntersectWith(page.Items.Select(row => row.Id));
                _displayedQuery = _requestedQuery = query;
                _page = page;
                _pageSize = query.Top;
                _loadFailed = false;
                _error = null;
                return true;
            }
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (ReferenceEquals(load, _activeLoad))
            {
                _pageSize = _displayedQuery.Top;
                _loadFailed = true;
                _error = "The Series grid could not be refreshed.";
            }
        }
        finally
        {
            if (ReferenceEquals(load, _activeLoad))
            {
                _activeLoad = null;
                _loading = false;
            }
        }
        return false;
    }

    async Task<WorkbenchSeriesPage> ReadPageAsync(GridQuery query, CancellationToken cancellation)
    {
        WorkbenchSeriesSearch search = new(
            query.Search,
            query.State,
            Enum.TryParse(query.Review, out WorkbenchReview review) ? review : null,
            query.Sort,
            query.Descending,
            query.Skip,
            query.Top,
            query.ColumnFilters
        );
        using HttpResponseMessage response = await Http.PostAsJsonAsync(
            "api/workbench/series/query",
            search,
            cancellation
        );
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkbenchSeriesPage>(cancellation)
            ?? throw new JsonException("Missing series page.");
    }

    void ToggleSeries(Guid id)
    {
        if (!_expanded.Add(id))
            _expanded.Remove(id);
    }

    void StartCreate()
    {
        if (_loading || Stations.Count == 0)
            return;

        ClearMutationMessages();
        _draft = new();
        UseStationDateRange();
    }

    void StartEdit(WorkbenchSeries row)
    {
        if (_loading)
            return;

        int index = 0;
        while (
            index < Stations.Count
            && (Stations[index].State != row.State || Stations[index].Name != row.Station)
        )
            index++;

        ClearMutationMessages();
        if (index == Stations.Count)
        {
            _error = "The station for this Series is unavailable in the current archive.";
            return;
        }

        _expanded.Remove(row.Id);
        _draft = new()
        {
            Id = row.Id,
            Name = row.Name,
            StationIndex = index,
            From = row.From,
            To = row.To,
            Review = row.Review,
            Notes = row.Notes,
            ObservationCount = row.ObservationCount,
            Version = row.Version,
        };
    }

    void UseStationDateRange()
    {
        if (_draft is not { } draft)
            return;

        WorkbenchStation station = Stations[draft.StationIndex];
        draft.From = station.FirstDay;
        draft.To = station.LastDay;
    }

    void StartDelete(WorkbenchSeries row)
    {
        if (_loading)
            return;

        ClearMutationMessages();
        _deleting = row.Id;
    }

    void CancelEdit()
    {
        _draft = null;
        _error = null;
    }

    void CancelDelete() => _deleting = null;

    void ClearMutationMessages()
    {
        _deleting = null;
        _error = null;
        _loadFailed = false;
        _notice = null;
    }

    async Task SaveAsync()
    {
        if (_draft is not { } draft || _busy || _lifetime.IsCancellationRequested)
            return;

        WorkbenchStation station = Stations[draft.StationIndex];
        if (string.IsNullOrWhiteSpace(draft.Name))
        {
            _error = "Enter a name for the Series.";
            return;
        }
        if (draft.From > draft.To || draft.From < station.FirstDay || draft.To > station.LastDay)
        {
            _error = "Enter a date range within the station’s published coverage.";
            return;
        }

        WorkbenchSeriesDraft body = new(
            draft.Name.Trim(),
            station.State,
            station.Name,
            draft.From,
            draft.To,
            draft.Review,
            draft.Notes,
            draft.Version
        );
        string path = draft.Id is Guid id ? $"api/workbench/series/{id:D}" : "api/workbench/series";
        await MutateAsync(
            cancellation =>
                draft.Id.HasValue
                    ? Http.PutAsJsonAsync(path, body, cancellation)
                    : Http.PostAsJsonAsync(path, body, cancellation),
            draft.Id.HasValue ? "Series updated." : "Series created."
        );
    }

    Task DeleteAsync(WorkbenchSeries row) =>
        MutateAsync(
            cancellation =>
                Http.DeleteAsync(
                    FormattableString.Invariant(
                        $"api/workbench/series/{row.Id:D}?version={row.Version}"
                    ),
                    cancellation
                ),
            "Series deleted."
        );

    async Task MutateAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> request,
        string success
    )
    {
        if (_loading || _busy || _lifetime.IsCancellationRequested)
            return;

        _busy = true;
        _error = null;
        _notice = null;
        try
        {
            using HttpResponseMessage response = await request(_lifetime.Token);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                _draft = null;
                _deleting = null;
                await ReloadAsync();
                _error ??=
                    "This Series changed in another tab. Review the latest data and try again.";
            }
            else if (!response.IsSuccessStatusCode)
            {
                _error = await ResponseErrorAsync(response);
            }
            else
            {
                _draft = null;
                _deleting = null;
                _notice = success;
                await ReloadAsync();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            _error =
                "The change could not be completed. Reload the grid and check the current Series.";
        }
        finally
        {
            _busy = false;
        }
    }

    async Task<string> ResponseErrorAsync(HttpResponseMessage response)
    {
        try
        {
            ProblemMessage? problem = await response.Content.ReadFromJsonAsync<ProblemMessage>(
                _lifetime.Token
            );
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                return problem.Detail;
        }
        catch (Exception error) when (error is NotSupportedException or JsonException) { }

        return response.StatusCode == HttpStatusCode.NotFound
            ? "This Series is no longer available. Reload the grid and try again."
            : "The workbench did not accept the change. Check the Series details and try again.";
    }

    static string ReviewLabel(WorkbenchReview review) =>
        review switch
        {
            WorkbenchReview.Pending => "Pending",
            WorkbenchReview.InReview => "In review",
            WorkbenchReview.Reviewed => "Reviewed",
            _ => review.ToString(),
        };

    static string ReviewClass(WorkbenchReview review) =>
        review switch
        {
            WorkbenchReview.InReview => "wb-review wb-review-active",
            WorkbenchReview.Reviewed => "wb-review wb-review-done",
            _ => "wb-review",
        };

    static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static string Count(long number) => number.ToString("N0", CultureInfo.CurrentCulture);

    public void Dispose()
    {
        AssistantWorkspace.Remove(this);
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    sealed class SeriesEditor
    {
        public Guid? Id { get; init; }
        public string Name { get; set; } = "";
        public int StationIndex { get; set; }
        public DateOnly From { get; set; }
        public DateOnly To { get; set; }
        public WorkbenchReview Review { get; set; }
        public string Notes { get; set; } = "";
        public long ObservationCount { get; init; }
        public long? Version { get; init; }
    }

    sealed record ProblemMessage(string? Detail);

    sealed record GridQuery(
        WorkbenchSeriesSort Sort = WorkbenchSeriesSort.Name,
        bool Descending = false,
        int Skip = 0,
        int Top = 20,
        string Search = "",
        string State = "",
        string Review = "",
        IReadOnlyList<ColumnFilterGroup>? ColumnFilters = null
    );
}
