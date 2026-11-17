using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Radiation;

public partial class RadNetResults : IDisposable
{
    const int ActionColumnWidth = 11;

    static readonly IReadOnlyDictionary<RadNetEntity, ResultColumn[]> Columns = new Dictionary<
        RadNetEntity,
        ResultColumn[]
    >
    {
        [RadNetEntity.Station] =
        [
            new(nameof(RadNetRow.StationName), "Station", 14),
            new(nameof(RadNetRow.State), "State", 6),
            new(nameof(RadNetRow.StationId), "Station ID", 9, true),
        ],
        [RadNetEntity.SourceFile] =
        [
            new(nameof(RadNetRow.StationName), "Station", 12),
            new(nameof(RadNetRow.Year), "Year", 6, true),
            new(nameof(RadNetRow.FileName), "Source file", 16),
            new(nameof(RadNetRow.SourceRows), "Observations", 9, true),
        ],
        [RadNetEntity.Observation] =
        [
            new(nameof(RadNetRow.ObservedAt), "Observed at (UTC)", 14),
            new(nameof(RadNetRow.StationName), "Station", 11),
            new(nameof(RadNetRow.Status), "Status", 8),
            new(nameof(RadNetRow.DoseNsvH), "Dose (nSv/h)", 9, true),
            new(nameof(RadNetRow.ExposureMrH), "Exposure (mR/h)", 11, true),
            new(nameof(RadNetRow.SourceRow), "CSV row", 8, true),
        ],
        [RadNetEntity.Channel] =
        [
            new(nameof(RadNetRow.Channel), "Channel", 7),
            new(nameof(RadNetRow.CountRate), "Count rate (CPM)", 12, true),
            new(nameof(RadNetRow.ObservedAt), "Observed at (UTC)", 14),
            new(nameof(RadNetRow.StationName), "Station", 13),
        ],
    };

    static readonly ResultColumn[] ObservationColumns = Columns[RadNetEntity.Observation];
    static readonly ResultColumn[] ObservationColumnsWithState =
    [
        .. ObservationColumns[..2],
        new(nameof(RadNetRow.State), "State", 6),
        .. ObservationColumns[2..],
    ];

    CancellationTokenSource? _pendingRequest;
    readonly string _filterEditorId = $"radnet-filter-{Guid.NewGuid():N}";
    RadNetQuery? _lastQuery;
    string? _lastQueryUrl;
    ResultRequest _requested = new(new(), []);
    ResultRequest _displayed = new(new(), []);
    ResultRequest? _assistantRequest;
    RadNetQueryResult? _result;
    ResultColumn? _editingColumn;
    string? _error;
    bool _loading;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Inject]
    SiteAssistantWorkspace AssistantWorkspace { get; set; } = null!;

    [Parameter, EditorRequired]
    public RadNetQuery Query { get; set; } = new();

    [Parameter]
    public string QueryUrl { get; set; } = "api/radnet/query";

    [Parameter]
    public string EmptyText { get; set; } = "No records match these filters.";

    [Parameter]
    public EventCallback OnConflict { get; set; }

    [Parameter]
    public Func<RadNetRow, string?>? SourceLink { get; set; }

    [Parameter]
    public bool ShowObservationState { get; set; }

    [Parameter]
    public AssistantSurface AssistantGrid { get; set; }

    [Parameter]
    public EventCallback<RadNetRow> OnOpen { get; set; }

    [Parameter]
    public EventCallback<RadNetRow> OnQuality { get; set; }

    [Parameter]
    public EventCallback<(string Field, bool Descending)> OnSort { get; set; }

    [Parameter]
    public bool OpenQuality { get; set; }

    RadNetQuery DisplayedQuery => _displayed.Query;
    IReadOnlyList<ResultColumn> DisplayedColumns =>
        ShowObservationState && DisplayedQuery.Entity == RadNetEntity.Observation
            ? ObservationColumnsWithState
            : Columns[DisplayedQuery.Entity];
    int MinimumTableWidth =>
        DisplayedColumns.Sum(column => column.Width) + (HasActions ? ActionColumnWidth : 0);
    bool ShowQuality => OnQuality.HasDelegate && DisplayedQuery.Entity != RadNetEntity.Station;
    bool HasActions =>
        ShowQuality || (OnOpen.HasDelegate && DisplayedQuery.Entity != RadNetEntity.Channel);

    string OpenText =>
        DisplayedQuery.Entity switch
        {
            RadNetEntity.Station => "Files",
            RadNetEntity.SourceFile => OpenQuality ? "Quality" : "Observations",
            RadNetEntity.Observation => "Channels",
            _ => "Open",
        };

    protected override Task OnParametersSetAsync()
    {
        bool sameQuery = Query == _lastQuery;
        if (sameQuery && QueryUrl == _lastQueryUrl)
        {
            if (!_loading)
                UpdateAssistantTarget();
            return Task.CompletedTask;
        }

        AssistantWorkspace.Remove(this);
        _lastQuery = Query;
        _lastQueryUrl = QueryUrl;
        if (sameQuery)
        {
            ResultRequest current =
                _result is not null
                && (_error is not null || ReferenceEquals(_requested, _assistantRequest))
                    ? _displayed
                    : _requested;
            return LoadAsync(current with { Query = current.Query with { Skip = 0 } });
        }

        _result = null;
        _editingColumn = null;
        return LoadAsync(new(Query, []));
    }

    void UpdateAssistantTarget()
    {
        if (
            _result is null
            || Query.Entity != RadNetEntity.Observation
            || AssistantGrid
                is not (
                    AssistantSurface.WorkbenchProjectObservations
                    or AssistantSurface.WorkbenchArchiveObservations
                )
        )
        {
            AssistantWorkspace.Remove(this);
            return;
        }

        object scope = (Query, QueryUrl);
        AssistantWorkspace.Set(
            this,
            new(AssistantPage.Workbench, AssistantGrid),
            action => ApplyAssistantAsync(action, scope),
            scope
        );
    }

    bool HasAssistantScope(object scope) =>
        AssistantWorkspace.Current is { } target
        && ReferenceEquals(target.Owner, this)
        && Equals(target.Scope, scope);

    async Task<bool> ApplyAssistantAsync(AssistantAction action, object scope)
    {
        bool applied = false;
        await InvokeAsync(async () =>
        {
            if (_loading || _result is null || !HasAssistantScope(scope))
                return;

            ResultRequest? request = action switch
            {
                { Kind: AssistantActionKind.ClearGrid } => new(
                    DisplayedQuery with
                    {
                        Skip = 0,
                    },
                    []
                ),
                {
                    Kind: AssistantActionKind.ApplyWorkbenchGridQuery,
                    WorkbenchGridQuery: { } changes
                } => new(
                    DisplayedQuery with
                    {
                        SortField = changes.SortField ?? DisplayedQuery.SortField,
                        Descending = changes.SortField is null
                            ? DisplayedQuery.Descending
                            : changes.Descending,
                        Skip = 0,
                        Top = changes.PageSize ?? DisplayedQuery.Top,
                    },
                    [
                        .. _displayed.ColumnFilters.Where(filter =>
                            !changes.Filters.Any(change => SameField(filter.Field, change.Field))
                        ),
                        .. changes.Filters,
                    ]
                ),
                _ => null,
            };
            if (request is null)
                return;

            _editingColumn = null;
            _assistantRequest = request;
            try
            {
                Task load = LoadAsync(request);
                StateHasChanged();
                await load;
                applied = ReferenceEquals(_displayed, request) && HasAssistantScope(scope);
                StateHasChanged();
            }
            finally
            {
                if (ReferenceEquals(_assistantRequest, request))
                    _assistantRequest = null;
            }
        });
        return applied;
    }

    string OpenLabel(RadNetRow row)
    {
        string name = DisplayedQuery.Entity switch
        {
            RadNetEntity.SourceFile => row.FileName ?? row.StationName,
            RadNetEntity.Observation => RadNetPresentation.Timestamp(row.ObservedAt),
            _ => row.StationName,
        };
        return $"{OpenText} for {name}";
    }

    string? SortDirection(ResultColumn column) =>
        string.Equals(DisplayedQuery.SortField, column.Field, StringComparison.OrdinalIgnoreCase)
            ? DisplayedQuery.Descending
                ? "descending"
                : "ascending"
            : null;

    async Task SortAsync(ResultColumn column)
    {
        if (_loading)
            return;

        bool descending =
            string.Equals(
                DisplayedQuery.SortField,
                column.Field,
                StringComparison.OrdinalIgnoreCase
            ) && !DisplayedQuery.Descending;
        ResultRequest request = _displayed with
        {
            Query = DisplayedQuery with
            {
                SortField = column.Field,
                Descending = descending,
                Skip = 0,
            },
        };
        RadNetQuery? parentQuery = _lastQuery;
        await OnSort.InvokeAsync((column.Field, descending));
        if (_lastQuery == parentQuery)
            await LoadAsync(request);
    }

    Task PageAsync(int direction) =>
        _loading
            ? Task.CompletedTask
            : LoadAsync(
                _displayed with
                {
                    Query = DisplayedQuery with
                    {
                        Skip = Math.Max(0, DisplayedQuery.Skip + direction * DisplayedQuery.Top),
                    },
                }
            );

    static bool SameField(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    static RadNetField FindField(string name) =>
        RadNetCatalog.Fields.First(field => SameField(field.Name, name));

    ColumnFilterGroup? AppliedFilter(string field) =>
        _displayed.ColumnFilters.FirstOrDefault(filter => SameField(filter.Field, field));

    void OpenFilter(ResultColumn column)
    {
        if (!_loading)
            _editingColumn = _editingColumn == column ? null : column;
    }

    void CloseFilter() => _editingColumn = null;

    Task ApplyFilterAsync(ColumnFilterGroup filter) =>
        SetColumnFiltersAsync([
            .. _displayed.ColumnFilters.Where(item => !SameField(item.Field, filter.Field)),
            filter,
        ]);

    Task RemoveFilterAsync() =>
        SetColumnFiltersAsync(
            _displayed
                .ColumnFilters.Where(filter => !SameField(filter.Field, _editingColumn?.Field))
                .ToArray()
        );

    Task ClearFiltersAsync()
    {
        CloseFilter();
        return SetColumnFiltersAsync([]);
    }

    Task SetColumnFiltersAsync(IReadOnlyList<ColumnFilterGroup> filters)
    {
        if (_loading)
            return Task.CompletedTask;

        return LoadAsync(
            _displayed with
            {
                Query = DisplayedQuery with { Skip = 0 },
                ColumnFilters = filters,
            }
        );
    }

    static RadNetFilterGroup ToRadNetFilter(ColumnFilterGroup filter) =>
        new(
            FindField(filter.Field).Entity,
            filter.MatchAny,
            filter
                .Conditions.Select(condition => new RadNetCondition(
                    filter.Field,
                    condition.Operator,
                    condition.Value
                ))
                .ToArray()
        );

    async Task LoadAsync(ResultRequest request)
    {
        _pendingRequest?.Cancel();
        using CancellationTokenSource cancellation = new();
        _pendingRequest = cancellation;
        _requested = request;
        _loading = true;
        _error = null;

        try
        {
            RadNetQuery query = request.Query;
            if (request.ColumnFilters.Count > 0)
                query = query with
                {
                    Filters =
                    [
                        .. query.Filters ?? [],
                        .. request.ColumnFilters.Select(ToRadNetFilter),
                    ],
                };

            using HttpResponseMessage response = await Http.PostAsJsonAsync(
                QueryUrl,
                query,
                cancellation.Token
            );
            if (!ReferenceEquals(_pendingRequest, cancellation))
                return;
            if (response.StatusCode == HttpStatusCode.Conflict && OnConflict.HasDelegate)
            {
                await OnConflict.InvokeAsync();
                if (ReferenceEquals(_pendingRequest, cancellation))
                    _error = "The project changed. Retry to refresh its observations.";
                return;
            }
            if (!response.IsSuccessStatusCode)
            {
                string message =
                    response.StatusCode == HttpStatusCode.BadRequest
                        ? (
                            await response.Content.ReadFromJsonAsync<QueryProblem>(
                                cancellation.Token
                            )
                        )?.Title
                            ?? "Check the field, comparison, and value in each filter."
                        : "The archive query could not be completed.";
                if (ReferenceEquals(_pendingRequest, cancellation))
                    _error = message;
                return;
            }

            RadNetQueryResult result = await response.Content.ReadFromJsonAsync<RadNetQueryResult>(
                cancellation.Token
            ) ?? throw new JsonException("Missing archive query results.");
            if (ReferenceEquals(_pendingRequest, cancellation))
            {
                (_displayed, _result) = (request, result);
                UpdateAssistantTarget();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (ReferenceEquals(_pendingRequest, cancellation))
                _error = "The archive query could not be completed.";
        }
        finally
        {
            if (ReferenceEquals(_pendingRequest, cancellation))
            {
                _pendingRequest = null;
                _loading = false;
            }
        }
    }

    static string CellClass(ResultColumn column) =>
        column.Numeric ? "radnet-numeric"
        : column.Field == nameof(RadNetRow.FileName) ? "radnet-file-name"
        : "";

    static string FormatCell(RadNetRow row, string field) =>
        field switch
        {
            nameof(RadNetRow.StationName) => row.StationName,
            nameof(RadNetRow.State) => row.State,
            nameof(RadNetRow.StationId) => RadNetPresentation.Count(row.StationId),
            nameof(RadNetRow.Year) => row.Year?.ToString(CultureInfo.InvariantCulture) ?? "—",
            nameof(RadNetRow.FileName) => row.FileName ?? "—",
            nameof(RadNetRow.SourceRows) => RadNetPresentation.Count(row.SourceRows),
            nameof(RadNetRow.SourceRow) => RadNetPresentation.Count(row.SourceRow),
            nameof(RadNetRow.ObservedAt) => RadNetPresentation.Timestamp(row.ObservedAt),
            nameof(RadNetRow.Status) => string.IsNullOrWhiteSpace(row.Status) ? "—" : row.Status,
            nameof(RadNetRow.DoseNsvH) => RadNetPresentation.Rate(row.DoseNsvH),
            nameof(RadNetRow.ExposureMrH) => RadNetPresentation.Rate(row.ExposureMrH),
            nameof(RadNetRow.Channel) => row.Channel ?? "—",
            nameof(RadNetRow.CountRate) => RadNetPresentation.Rate(row.CountRate),
            _ => "—",
        };

    public void Dispose()
    {
        AssistantWorkspace.Remove(this);
        _pendingRequest?.Cancel();
        _pendingRequest = null;
        GC.SuppressFinalize(this);
    }

    sealed record ResultColumn(string Field, string Label, int Width, bool Numeric = false);

    sealed record ResultRequest(RadNetQuery Query, IReadOnlyList<ColumnFilterGroup> ColumnFilters);

    sealed record QueryProblem(string? Title);
}
