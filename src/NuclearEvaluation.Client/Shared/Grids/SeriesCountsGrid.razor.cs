using System.Text.Json;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class SeriesCountsGrid
{
    [Inject]
    public INuclearEvaluationApi Api { get; set; } = null!;

    [Inject]
    public IGridResultCache ResultCache { get; set; } = null!;

    SeriesCountsView? _summary;
    string? _summaryKey;
    int _sequence;
    bool _isLoading = true;
    bool _hasError;

    public async Task RefreshSummaryData(DataQuery query)
    {
        int sequence = ++_sequence;
        string key = CacheKey(query);
        _isLoading = true;
        _hasError = false;
        await InvokeAsync(StateHasChanged);

        // Show last known totals from the browser cache immediately, then refresh.
        GridCacheHit<SeriesCountsView> cached = await ResultCache.TryGetAsync<SeriesCountsView>(
            key
        );
        if (sequence != _sequence)
            return;
        if (cached.Found && cached.Entries.Count > 0)
        {
            _summary = cached.Entries[0];
            _summaryKey = key;
            await InvokeAsync(StateHasChanged);
        }

        SeriesCountsView fresh;
        try
        {
            fresh = await Api.GetSeriesCounts(query);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            if (sequence != _sequence)
                return;
            if (_summaryKey != key)
            {
                _summary = null;
                _summaryKey = null;
            }
            _isLoading = false;
            _hasError = true;
            await InvokeAsync(StateHasChanged);
            return;
        }

        // A newer query superseded this one; discard the stale result.
        if (sequence != _sequence)
        {
            return;
        }

        _hasError = false;
        _summary = fresh;
        _summaryKey = key;
        _isLoading = false;
        await InvokeAsync(StateHasChanged);

        await ResultCache.SetAsync(key, new List<SeriesCountsView> { fresh }, 1);
    }

    static string FormatTotal(int? total) => total?.ToString("N0") ?? "—";

    static string CacheKey(DataQuery query)
    {
        // Grid paging cannot change totals. Ordering and priorities can change a limited set.
        bool limited = query.SeriesQuery?.Limit is not null;
        string scope = JsonSerializer.Serialize(
            new
            {
                query.Filter,
                query.PresetFilterBox,
                query.SeriesQuery,
                query.AssistantQuery,
                query.ProjectId,
                query.DecayCorrected,
                OrderBy = limited ? query.OrderBy : null,
                PriorityIds = limited ? query.PriorityIds : null,
            }
        );
        return $"series-counts|{scope}";
    }
}
