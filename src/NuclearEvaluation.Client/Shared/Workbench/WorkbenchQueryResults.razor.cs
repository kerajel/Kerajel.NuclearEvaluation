using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using static NuclearEvaluation.Client.Shared.Radiation.RadNetPresentation;

namespace NuclearEvaluation.Client.Shared.Workbench;

public partial class WorkbenchQueryResults : IDisposable
{
    static readonly ResultColumn FileColumn = new(
        nameof(WorkbenchQueryRow.FileName),
        "Source file",
        20,
        row => row.FileName,
        Source: true
    );
    static readonly ResultColumn TimeColumn = new(
        nameof(WorkbenchQueryRow.ObservedAt),
        "Observed at (UTC)",
        14,
        row => Timestamp(row.ObservedAt)
    );
    static readonly ResultColumn StationColumn = new(
        nameof(WorkbenchQueryRow.Station),
        "Station",
        12,
        row => row.Station
    );
    static readonly ResultColumn StateColumn = new(
        nameof(WorkbenchQueryRow.State),
        "State",
        6,
        row => row.State
    );
    static readonly ResultColumn SourceRowColumn = new(
        nameof(WorkbenchQueryRow.SourceRow),
        "CSV row",
        7,
        row => Count(row.SourceRow),
        Numeric: true
    );
    static readonly ResultColumn MatchingColumn = new(
        nameof(WorkbenchQueryRow.MatchingObservations),
        "Matching observations",
        12,
        row => Count(row.MatchingObservations),
        Numeric: true
    );
    static readonly (string Value, string Label)[] ReviewValues = Enum.GetValues<WorkbenchReview>()
        .Select(review =>
            (
                review.ToString(),
                review == WorkbenchReview.InReview ? "In review" : review.ToString()
            )
        )
        .ToArray();
    static readonly IReadOnlyDictionary<WorkbenchQueryEntity, ResultColumn[]> Columns =
        new Dictionary<WorkbenchQueryEntity, ResultColumn[]>
        {
            [WorkbenchQueryEntity.Project] =
            [
                new(nameof(WorkbenchQueryRow.ProjectName), "Project", 24, row => row.ProjectName),
                new(
                    nameof(WorkbenchQueryRow.ProjectSeriesCount),
                    "Linked series",
                    11,
                    row => Count(row.ProjectSeriesCount),
                    Numeric: true
                ),
            ],
            [WorkbenchQueryEntity.Series] =
            [
                new(nameof(WorkbenchQueryRow.SeriesName), "Series", 15, row => row.SeriesName),
                StationColumn,
                StateColumn,
                new(nameof(WorkbenchQueryRow.From), "From (UTC)", 9, row => Date(row.From)),
                new(nameof(WorkbenchQueryRow.To), "Through (UTC)", 9, row => Date(row.To)),
                new(nameof(WorkbenchQueryRow.Review), "Review", 8, row =>
                    row.Review == WorkbenchReview.InReview ? "In review" : row.Review?.ToString()
                ),
                new(
                    nameof(WorkbenchQueryRow.SeriesObservationCount),
                    "Series observations",
                    11,
                    row => Count(row.SeriesObservationCount),
                    Numeric: true
                ),
            ],
            [WorkbenchQueryEntity.File] =
            [
                FileColumn,
                StationColumn,
                StateColumn,
                new(
                    nameof(WorkbenchQueryRow.Year),
                    "Year",
                    8,
                    row => row.Year?.ToString(CultureInfo.InvariantCulture),
                    Numeric: true
                ),
            ],
            [WorkbenchQueryEntity.Observation] =
            [
                TimeColumn,
                StationColumn,
                new(nameof(WorkbenchQueryRow.Status), "Status", 8, row => row.Status),
                new(
                    nameof(WorkbenchQueryRow.ExposureMrH),
                    "Exposure (mR/h)",
                    9,
                    row => Rate(row.ExposureMrH),
                    Numeric: true
                ),
                new(
                    nameof(WorkbenchQueryRow.DoseNsvH),
                    "Dose (nSv/h)",
                    9,
                    row => Rate(row.DoseNsvH),
                    Numeric: true
                ),
                SourceRowColumn,
                FileColumn,
            ],
            [WorkbenchQueryEntity.Channel] =
            [
                new(nameof(WorkbenchQueryRow.Channel), "Channel", 8, row => row.Channel),
                new(
                    nameof(WorkbenchQueryRow.CountRate),
                    "Count rate (CPM)",
                    10,
                    row => Rate(row.CountRate),
                    Numeric: true
                ),
                TimeColumn,
                StationColumn,
                SourceRowColumn,
                FileColumn,
            ],
        };

    CancellationTokenSource? _pending;
    WorkbenchQuery? _lastQuery;
    WorkbenchQuery _requested = new();
    DisplayedPage? _page;
    ColumnFilterSelection? _filterEditor;
    bool _disposed;
    bool _notifySortOnRetry;
    string? _error;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    SiteAssistantWorkspace AssistantWorkspace { get; set; } = null!;

    [Parameter, EditorRequired]
    public WorkbenchQuery Query { get; set; } = new();

    [Parameter]
    public EventCallback<(string Field, bool Descending)> OnSort { get; set; }

    bool Loading => _pending is not null;

    protected override Task OnParametersSetAsync()
    {
        // Each run supplies a distinct query, even when its draft values are the same.
        if (ReferenceEquals(Query, _lastQuery))
            return Task.CompletedTask;

        _lastQuery = Query;
        _filterEditor = null;
        AssistantWorkspace.Remove(this);
        return LoadAsync(Query);
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (_page is not { } page || !ReferenceEquals(page.Scope, _lastQuery))
        {
            AssistantWorkspace.Remove(this);
            return;
        }

        WorkbenchQuery scope = page.Scope;
        AssistantWorkspace.Set(
            this,
            new(
                AssistantPage.Workbench,
                AssistantSurface.WorkbenchQueryResults,
                WorkbenchResultEntity: scope.Entity
            ),
            action => ApplyAssistantActionAsync(action, scope),
            scope
        );
    }

    async Task<bool> ApplyAssistantActionAsync(AssistantAction action, WorkbenchQuery scope)
    {
        bool applied = false;
        await InvokeAsync(async () =>
        {
            if (
                _disposed
                || !ReferenceEquals(scope, _lastQuery)
                || !ReferenceEquals(AssistantWorkspace.Current?.Owner, this)
                || Loading
                || _page is not { Query: WorkbenchQuery query }
            )
                return;

            WorkbenchQuery next;
            if (action.Kind == AssistantActionKind.ClearGrid)
            {
                _filterEditor = null;
                next = query with { ResultFilters = null, ResultFilterGroups = null };
            }
            else if (
                action is
                { Kind: AssistantActionKind.ApplyWorkbenchGridQuery, WorkbenchGridQuery: { } grid }
            )
            {
                next = WithResultFilters(query, grid.Filters) with
                {
                    SortField = grid.SortField ?? query.SortField,
                    Descending = grid.SortField is not null ? grid.Descending : query.Descending,
                    Top = grid.PageSize ?? query.Top,
                };
            }
            else
            {
                return;
            }

            Task<bool> load = LoadAsync(next with { Skip = 0 });
            StateHasChanged();
            applied = await load;
            if (!_disposed)
                StateHasChanged();
        });
        return applied;
    }

    Task SortAsync(string field)
    {
        if (Loading || _page is not { Query: WorkbenchQuery query })
            return Task.CompletedTask;

        bool descending =
            string.Equals(field, query.SortField, StringComparison.OrdinalIgnoreCase)
            && !query.Descending;
        return LoadAsync(query with { SortField = field, Descending = descending, Skip = 0 }, true);
    }

    Task PageAsync(int direction)
    {
        if (Loading || _page is not { Query: WorkbenchQuery query })
            return Task.CompletedTask;

        int skip = Math.Max(0, query.Skip + direction * query.Top);
        return LoadAsync(query with { Skip = skip });
    }

    void OpenFilter(WorkbenchQueryField field, string label)
    {
        if (Loading || _page is null)
            return;

        if (_filterEditor?.Field.Name == field.Name)
        {
            _filterEditor = null;
            return;
        }

        _filterEditor = new(field, label);
    }

    Task ApplyFilterAsync(ColumnFilterGroup filter)
    {
        if (Loading || _page is not { Query: WorkbenchQuery query })
            return Task.CompletedTask;

        return LoadAsync(WithResultFilters(query, [filter]) with { Skip = 0 });
    }

    static WorkbenchQuery WithResultFilters(
        WorkbenchQuery query,
        IReadOnlyList<ColumnFilterGroup> filters
    )
    {
        if (filters.Count == 0)
            return query;

        HashSet<string> fields = filters
            .Select(filter => filter.Field)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return query with
        {
            ResultFilters = query
                .ResultFilters?.Where(item => !fields.Contains(item.Field))
                .ToArray(),
            ResultFilterGroups =
            [
                .. (query.ResultFilterGroups ?? []).Where(item => !fields.Contains(item.Field)),
                .. filters,
            ],
        };
    }

    Task ClearFiltersAsync(string? field = null)
    {
        if (Loading || _page is not { Query: WorkbenchQuery query })
            return Task.CompletedTask;

        _filterEditor = null;
        return LoadAsync(
            query with
            {
                ResultFilters = field is null
                    ? null
                    : query.ResultFilters?.Where(item => !SameField(item.Field, field)).ToArray(),
                ResultFilterGroups = field is null
                    ? null
                    : query
                        .ResultFilterGroups?.Where(item => !SameField(item.Field, field))
                        .ToArray(),
                Skip = 0,
            }
        );
    }

    void CloseFilter() => _filterEditor = null;

    Task RetryAsync() => LoadAsync(_requested, _notifySortOnRetry);

    async Task<bool> LoadAsync(WorkbenchQuery query, bool notifySort = false)
    {
        WorkbenchQuery scope = _lastQuery ?? query;
        _pending?.Cancel();
        using CancellationTokenSource request = new();
        _pending = request;
        _requested = query;
        _notifySortOnRetry = notifySort;
        _error = null;

        bool committed = false;
        try
        {
            using HttpResponseMessage response = await Http.PostAsJsonAsync(
                "api/workbench/query",
                query,
                request.Token
            );
            if (!ReferenceEquals(_pending, request))
                return false;

            if (!response.IsSuccessStatusCode)
            {
                string message = "The workbench query could not be completed.";
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    QueryProblem? problem = await response.Content.ReadFromJsonAsync<QueryProblem>(
                        request.Token
                    );
                    message = problem?.Detail
                        ?? problem?.Title
                        ?? "Check the query filters and try again.";
                }
                if (ReferenceEquals(_pending, request))
                    _error = message;
                return false;
            }

            WorkbenchQueryPage page = await response.Content.ReadFromJsonAsync<WorkbenchQueryPage>(
                request.Token
            ) ?? throw new JsonException("Missing workbench query results.");
            if (ReferenceEquals(_pending, request))
            {
                _page = new(scope, query, page);
                committed = true;
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (ReferenceEquals(_pending, request))
                _error = "The workbench query could not be completed.";
        }
        finally
        {
            if (ReferenceEquals(_pending, request))
            {
                if (!committed && ReferenceEquals(scope, query))
                    _page = null;
                _pending = null;
            }
        }

        if (committed && notifySort && query.SortField is { } sortField)
            await OnSort.InvokeAsync((sortField, query.Descending));
        return committed;
    }

    static IEnumerable<ResultColumn> ResultColumns(WorkbenchQuery query) =>
        query.Entity == WorkbenchQueryEntity.Series
        && (
            query.MinimumMatchingObservations.HasValue
            || SameField(query.SortField, MatchingColumn.Field)
            || FindResultFilter(query, MatchingColumn.Field) is not null
            || query.Filters?.Any(group =>
                group.Entity >= WorkbenchQueryEntity.File && group.Conditions.Count > 0
            ) == true
        )
            ? Columns[query.Entity].Append(MatchingColumn)
            : Columns[query.Entity];

    static WorkbenchQueryField? ResultField(WorkbenchQueryEntity entity, ResultColumn column) =>
        WorkbenchQueryCatalog.ResultFields.FirstOrDefault(field =>
            field.Entity == entity && field.Name == column.Field
        );

    static bool SameField(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    static ColumnFilterGroup? FindResultFilter(WorkbenchQuery query, string field)
    {
        if (
            query.ResultFilterGroups?.FirstOrDefault(item => SameField(item.Field, field)) is
            { } group
        )
            return group;

        ColumnFilterCondition[] conditions = query
            .ResultFilters?.Where(item => SameField(item.Field, field))
            .Take(ColumnFilterGroup.MaxConditions)
            .Select(item => new ColumnFilterCondition(item.Operator, item.Value))
            .ToArray()
            ?? [];
        return conditions.Length == 0 ? null : new(field, false, conditions);
    }

    static string? SortDirection(WorkbenchQuery query, ResultColumn column) =>
        string.Equals(query.SortField, column.Field, StringComparison.OrdinalIgnoreCase)
            ? query.Descending
                ? "descending"
                : "ascending"
            : null;

    static string EntityName(WorkbenchQueryEntity entity) =>
        entity switch
        {
            WorkbenchQueryEntity.Project => "Projects",
            WorkbenchQueryEntity.Series => "Series",
            WorkbenchQueryEntity.File => "Annual source files",
            WorkbenchQueryEntity.Observation => "Observations",
            WorkbenchQueryEntity.Channel => "Gamma readings",
            _ => throw new ArgumentOutOfRangeException(nameof(entity)),
        };

    static string? Date(DateOnly? date) =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    static string SourceUrl(int fileId) =>
        FormattableString.Invariant($"radiation?view=quality&file={fileId}");

    public void Dispose()
    {
        _disposed = true;
        AssistantWorkspace.Remove(this);
        _pending?.Cancel();
        _pending = null;
        GC.SuppressFinalize(this);
    }

    sealed record DisplayedPage(
        WorkbenchQuery Scope,
        WorkbenchQuery Query,
        WorkbenchQueryPage Result
    );

    sealed record QueryProblem(string? Title, string? Detail);

    sealed record ColumnFilterSelection(WorkbenchQueryField Field, string Label);

    sealed record ResultColumn(
        string Field,
        string Label,
        int Width,
        Func<WorkbenchQueryRow, string?> Format,
        bool Numeric = false,
        bool Source = false
    );
}
