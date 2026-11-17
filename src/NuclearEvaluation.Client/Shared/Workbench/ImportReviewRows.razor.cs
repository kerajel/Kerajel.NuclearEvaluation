using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;
using static NuclearEvaluation.Client.Shared.Radiation.RadNetPresentation;

namespace NuclearEvaluation.Client.Shared.Workbench;

public partial class ImportReviewRows : IDisposable
{
    static readonly ImportReviewDisposition[] Outcomes = Enum.GetValues<ImportReviewDisposition>();
    static readonly IReadOnlyList<(string Value, string Label)> OutcomeValues = Outcomes
        .Select(outcome => (outcome.ToString(), OutcomeLabel(outcome)))
        .ToArray();
    static readonly IReadOnlyList<ImportReviewField> Columns = ImportReviewCatalog
        .Fields.OrderBy(field =>
            field.Name switch
            {
                nameof(ImportReviewRow.SourceRow) => -5,
                nameof(ImportReviewRow.ObservedAt) => -4,
                nameof(ImportReviewRow.Disposition) => -3,
                nameof(ImportReviewRow.Reason) => -2,
                nameof(ImportReviewRow.DoseNsvH) => -1,
                _ => 0,
            }
        )
        .ToArray();
    static readonly ImportReviewQuery InitialQuery = new(nameof(ImportReviewRow.SourceRow));
    CancellationTokenSource? _pending;
    DisplayedPage? _page;
    ImportReviewQuery _lastRequested = InitialQuery;
    ImportReviewField? _editor;
    string? _error;
    Guid? _lastStageId;
    bool _expired;

    [Inject]
    HttpClient Http { get; set; } = null!;

    [Parameter, EditorRequired]
    public Guid StageId { get; set; }

    bool Busy => _pending is not null;

    protected override Task OnParametersSetAsync()
    {
        if (_lastStageId == StageId)
            return Task.CompletedTask;
        _lastStageId = StageId;
        _page = null;
        _editor = null;
        return LoadAsync(InitialQuery);
    }

    async Task LoadAsync(ImportReviewQuery query)
    {
        _pending?.Cancel();
        using CancellationTokenSource pending = new();
        _pending = pending;
        _lastRequested = query;
        _error = null;
        _expired = false;
        try
        {
            using HttpResponseMessage response = await Http.PostAsJsonAsync(
                $"api/workbench/import-review/{StageId:D}/query",
                query,
                pending.Token
            );
            if (!ReferenceEquals(_pending, pending))
                return;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _expired = true;
                _error = "This review is no longer available. Stage the CSV again.";
                return;
            }
            response.EnsureSuccessStatusCode();
            ImportReviewPage result = await response.Content.ReadFromJsonAsync<ImportReviewPage>(
                pending.Token
            ) ?? throw new JsonException("Missing staged-record page.");
            if (ReferenceEquals(_pending, pending))
                _page = new(query, result);
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested) { }
        catch (Exception error)
            when (error
                    is HttpRequestException
                        or IOException
                        or JsonException
                        or OperationCanceledException
            )
        {
            if (ReferenceEquals(_pending, pending))
                _error = "Staged records could not be loaded.";
        }
        finally
        {
            if (ReferenceEquals(_pending, pending))
                _pending = null;
        }
    }

    Task RetryAsync() => LoadAsync(_lastRequested);

    Task SortAsync(string field) =>
        !Busy && !_expired && _page is { } page
            ? LoadAsync(
                page.Query with
                {
                    SortField = field,
                    Descending = page.Query.SortField == field && !page.Query.Descending,
                    Skip = 0,
                }
            )
            : Task.CompletedTask;

    Task MoveAsync(int step) =>
        !Busy && !_expired && _page is { } page
            ? LoadAsync(
                page.Query with
                {
                    Skip = Math.Max(0, page.Query.Skip + step * page.Query.Top),
                }
            )
            : Task.CompletedTask;

    void ToggleEditor(ImportReviewField column)
    {
        if (!Busy && !_expired)
            _editor = _editor?.Name == column.Name ? null : column;
    }

    ColumnFilterGroup? FindFilter(string field) =>
        _page?.Query.Filters?.FirstOrDefault(group => group.Field == field);

    bool IsSelectedOutcome(ImportReviewDisposition outcome) =>
        FindFilter(nameof(ImportReviewRow.Disposition)) is { Conditions: [var condition] }
        && condition.Operator == RadNetOperator.Equal
        && condition.Value == outcome.ToString();

    Task SelectOutcomeAsync(ImportReviewDisposition? outcome)
    {
        const string field = nameof(ImportReviewRow.Disposition);
        return outcome is { } value
            ? ApplyFilterAsync(new(field, false, [new(RadNetOperator.Equal, value.ToString())]))
            : RemoveFilterAsync(field);
    }

    Task ApplyFilterAsync(ColumnFilterGroup filter) => SetFilterAsync(filter.Field, filter);

    Task RemoveFilterAsync(string field) => SetFilterAsync(field, null);

    Task SetFilterAsync(string field, ColumnFilterGroup? replacement)
    {
        if (Busy || _expired || _page is not { } page)
            return Task.CompletedTask;
        List<ColumnFilterGroup> filters = (page.Query.Filters ?? [])
            .Where(filter => filter.Field != field)
            .ToList();
        if (replacement is not null)
            filters.Add(replacement);
        else if (filters.Count == (page.Query.Filters?.Count ?? 0))
            return Task.CompletedTask;
        _editor = null;
        return LoadAsync(page.Query with { Skip = 0, Filters = filters });
    }

    Task ClearFiltersAsync()
    {
        if (Busy || _expired || _page is not { } page)
            return Task.CompletedTask;
        _editor = null;
        return LoadAsync(page.Query with { Skip = 0, Filters = null });
    }

    static string? SortDirection(ImportReviewQuery query, string field) =>
        query.SortField == field ? (query.Descending ? "descending" : "ascending") : null;

    static string ColumnWidth(string field) =>
        field switch
        {
            nameof(ImportReviewRow.Reason) => "ir-finding-width",
            nameof(ImportReviewRow.Location)
            or nameof(ImportReviewRow.ObservedAt)
            or nameof(ImportReviewRow.Status)
            or nameof(ImportReviewRow.Disposition) => "ir-detail-width",
            _ => "ir-numeric-width",
        };

    static string OutcomeLabel(ImportReviewDisposition outcome) =>
        outcome == ImportReviewDisposition.MatchesArchive ? "Matches archive" : outcome.ToString();

    static string CellText(ImportReviewRow row, string field) =>
        field switch
        {
            nameof(ImportReviewRow.SourceRow) => Count(row.SourceRow),
            nameof(ImportReviewRow.Location) => ValueOrDash(row.Location),
            nameof(ImportReviewRow.ObservedAt) => Timestamp(row.ObservedAt),
            nameof(ImportReviewRow.Status) => ValueOrDash(row.Status),
            nameof(ImportReviewRow.ExposureMrH) => Rate(row.ExposureMrH),
            nameof(ImportReviewRow.DoseNsvH) => Rate(row.DoseNsvH),
            nameof(ImportReviewRow.R03) => Rate(row.R03),
            nameof(ImportReviewRow.Disposition) => OutcomeLabel(row.Disposition),
            nameof(ImportReviewRow.Reason) => ValueOrDash(row.Reason),
            _ => "—",
        };

    static string ValueOrDash(string value) => string.IsNullOrEmpty(value) ? "—" : value;

    static string PageLabel(DisplayedPage page)
    {
        if (page.Result.TotalCount == 0)
            return "0 matching records";
        long first = (long)page.Query.Skip + 1;
        long last = page.Query.Skip + page.Result.Items.Count;
        return FormattableString.Invariant(
            $"{first:N0}–{last:N0} of {page.Result.TotalCount:N0} matching records"
        );
    }

    public void Dispose()
    {
        _pending?.Cancel();
        _pending = null;
        GC.SuppressFinalize(this);
    }

    sealed record DisplayedPage(ImportReviewQuery Query, ImportReviewPage Result);
}
