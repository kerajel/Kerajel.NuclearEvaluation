using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;
using static NuclearEvaluation.Client.Shared.Radiochemistry.RemDbPresentation;

namespace NuclearEvaluation.Client.Shared.Radiochemistry;

public partial class RemDbResults : IDisposable
{
    static readonly IReadOnlyDictionary<RemDbEntity, ResultColumn[]> Columns = new Dictionary<
        RemDbEntity,
        ResultColumn[]
    >
    {
        [RemDbEntity.Site] =
        [
            new(nameof(RemDbRow.SiteName), "Site", 17),
            new(nameof(RemDbRow.Country), "Country", 12),
            new(nameof(RemDbRow.Latitude), "Latitude", 9, true),
            new(nameof(RemDbRow.Longitude), "Longitude", 9, true),
        ],
        [RemDbEntity.SamplingWindow] =
        [
            new(nameof(RemDbRow.Medium), "Medium", 13),
            new(nameof(RemDbRow.Treatment), "Treatment", 13),
            new(nameof(RemDbRow.BeganOn), "Sampling began", 12),
            new(nameof(RemDbRow.EndedOn), "Sampling ended", 12),
            new(nameof(RemDbRow.SiteName), "Site", 16),
            new(nameof(RemDbRow.Country), "Country", 11),
        ],
        [RemDbEntity.Measurement] =
        [
            new(nameof(RemDbRow.Nuclide), "Radionuclide", 10),
            new(nameof(RemDbRow.Activity), "Reported activity", 12, true),
            new(nameof(RemDbRow.Unit), "Unit", 8),
            new(nameof(RemDbRow.Qualifier), "Qualifier", 9),
            new(nameof(RemDbRow.ValueType), "Value type", 11),
            new(nameof(RemDbRow.Medium), "Medium", 13),
            new(nameof(RemDbRow.EndedOn), "Sampling ended", 12),
            new(nameof(RemDbRow.SiteName), "Site", 15),
            new(nameof(RemDbRow.Country), "Country", 10),
            new(nameof(RemDbRow.Laboratory), "Laboratory", 14),
            new(nameof(RemDbRow.Apparatus), "Apparatus", 14),
            new(nameof(RemDbRow.FileName), "Source CSV", 14),
        ],
    };

    CancellationTokenSource? _pending;
    RemDbQuery? _lastQuery;
    ResultRequest _requested = new(new(), []);
    DisplayedPage? _page;
    ResultColumn? _editor;
    string? _error;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Parameter, EditorRequired]
    public RemDbQuery Query { get; set; } = new();

    [Parameter]
    public EventCallback<RemDbRow> OnOpen { get; set; }

    [Parameter]
    public EventCallback<(string Field, bool Descending)> OnSort { get; set; }

    bool Loading => _pending is not null;

    protected override Task OnParametersSetAsync()
    {
        if (ReferenceEquals(Query, _lastQuery))
            return Task.CompletedTask;

        _lastQuery = Query;
        _page = null;
        _editor = null;
        return LoadAsync(new(Query, []));
    }

    static string OpenLabel(RemDbEntity entity, RemDbRow row) =>
        entity == RemDbEntity.Site
            ? $"Sampling windows at {SiteLabel(row)}"
            : $"Measurements for {Display(row.Medium)}, {DateRange(row)}";

    static string? SortDirection(RemDbQuery query, ResultColumn column) =>
        SameField(query.SortField, column.Field)
            ? (query.Descending ? "descending" : "ascending")
            : null;

    Task SortAsync(ResultColumn column)
    {
        if (Loading || _page is not { Request: ResultRequest current })
            return Task.CompletedTask;

        return LoadAsync(
            current with
            {
                Query = current.Query with
                {
                    SortField = column.Field,
                    Descending = SortDirection(current.Query, column) == "ascending",
                    Skip = 0,
                },
                NotifySort = true,
            }
        );
    }

    Task PageAsync(int direction)
    {
        if (Loading || _page is not { Request: ResultRequest current })
            return Task.CompletedTask;

        int skip = Math.Max(0, current.Query.Skip + direction * current.Query.Top);
        return LoadAsync(
            current with
            {
                Query = current.Query with { Skip = skip },
                NotifySort = false,
            }
        );
    }

    void OpenFilter(ResultColumn column)
    {
        if (!Loading)
            _editor = _editor == column ? null : column;
    }

    void CloseFilter() => _editor = null;

    Task ApplyFilterAsync(ColumnFilterGroup filter)
    {
        if (Loading || _page is not { Request: ResultRequest current })
            return Task.CompletedTask;

        return ApplyFiltersAsync(
            current,
            [.. current.ColumnFilters.Where(item => !SameField(item.Field, filter.Field)), filter]
        );
    }

    Task ClearFiltersAsync(string? field = null)
    {
        if (Loading || _page is not { Request: ResultRequest current })
            return Task.CompletedTask;

        IReadOnlyList<ColumnFilterGroup> filters = field is null
            ? []
            : current.ColumnFilters.Where(item => !SameField(item.Field, field)).ToArray();
        return ApplyFiltersAsync(current, filters);
    }

    Task ApplyFiltersAsync(ResultRequest current, IReadOnlyList<ColumnFilterGroup> filters) =>
        LoadAsync(new(current.Query with { Skip = 0 }, filters));

    Task RetryAsync() => LoadAsync(_requested);

    static RemDbQuery CompileColumnFilters(ResultRequest request) =>
        request.ColumnFilters.Count == 0
            ? request.Query
            : request.Query with
            {
                Filters =
                [
                    .. (request.Query.Filters ?? []),
                    .. request.ColumnFilters.Select(group => new RemDbFilterGroup(
                        FindField(group.Field).Entity,
                        group.MatchAny,
                        group
                            .Conditions.Select(condition => new RemDbCondition(
                                group.Field,
                                condition.Operator,
                                condition.Value
                            ))
                            .ToArray()
                    )),
                ],
            };

    async Task LoadAsync(ResultRequest query)
    {
        _pending?.Cancel();
        using CancellationTokenSource request = new();
        _pending = request;
        _requested = query;
        _error = null;

        bool committed = false;
        try
        {
            using HttpResponseMessage response = await Http.PostAsJsonAsync(
                "api/remdb/query",
                CompileColumnFilters(query),
                request.Token
            );
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                QueryProblem? problem = await response.Content.ReadFromJsonAsync<QueryProblem>(
                    request.Token
                );
                if (ReferenceEquals(_pending, request))
                    _error = problem?.Title
                        ?? "Check the field, comparison, and value in each filter.";
                return;
            }
            response.EnsureSuccessStatusCode();
            RemDbQueryResult? result = await response.Content.ReadFromJsonAsync<RemDbQueryResult>(
                request.Token
            );
            if (result is null)
                throw new JsonException("Missing archive query response.");
            if (ReferenceEquals(_pending, request))
            {
                _page = new(query, result);
                committed = true;
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error)
            when (error is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (ReferenceEquals(_pending, request))
                _error = "The archive query could not be completed.";
        }
        finally
        {
            if (ReferenceEquals(_pending, request))
                _pending = null;
        }

        if (committed && query.NotifySort && query.Query.SortField is { } sortField)
            await OnSort.InvokeAsync((sortField, query.Query.Descending));
    }

    static RemDbField FindField(string field) =>
        RemDbCatalog.Fields.First(item => SameField(item.Key, field));

    static ColumnFilterGroup? FindFilter(ResultRequest request, string field) =>
        request.ColumnFilters.FirstOrDefault(filter => SameField(filter.Field, field));

    static bool SameField(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    static string FormatCell(RemDbRow row, string field) =>
        field switch
        {
            nameof(RemDbRow.Country) => Display(row.Country),
            nameof(RemDbRow.SiteName) => SiteLabel(row),
            nameof(RemDbRow.Latitude) => Number(row.Latitude),
            nameof(RemDbRow.Longitude) => Number(row.Longitude),
            nameof(RemDbRow.Medium) => Display(row.Medium),
            nameof(RemDbRow.Treatment) => Display(row.Treatment),
            nameof(RemDbRow.BeganOn) => row.SourceBegin ?? Date(row.BeganOn),
            nameof(RemDbRow.EndedOn) => row.SourceEnd ?? Date(row.EndedOn),
            nameof(RemDbRow.Nuclide) => Display(row.Nuclide),
            nameof(RemDbRow.Activity) => Number(row.Activity),
            nameof(RemDbRow.Unit) => Display(row.Unit),
            nameof(RemDbRow.Qualifier) => Display(row.Qualifier),
            nameof(RemDbRow.ValueType) => Display(row.ValueType),
            nameof(RemDbRow.Laboratory) => Display(row.Laboratory),
            nameof(RemDbRow.Apparatus) => Display(row.Apparatus),
            _ => Display(row.FileName),
        };

    public void Dispose()
    {
        _pending?.Cancel();
        _pending = null;
        GC.SuppressFinalize(this);
    }

    sealed record ResultColumn(string Field, string Label, int Width, bool Numeric = false);

    sealed record ResultRequest(
        RemDbQuery Query,
        IReadOnlyList<ColumnFilterGroup> ColumnFilters,
        bool NotifySort = false
    );

    sealed record DisplayedPage(ResultRequest Request, RemDbQueryResult Result);

    sealed record QueryProblem(string? Title);
}
