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

    SeriesCountsView[] _countSummary = [];
    int _sequence;
    bool _isLoading = true;
    bool _hasError;

    public async Task RefreshSummaryData(DataQuery query)
    {
        int sequence = ++_sequence;
        string key = $"series-counts|{JsonSerializer.Serialize(query)}";
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
            _countSummary = [cached.Entries[0]];
            await InvokeAsync(StateHasChanged);
        }

        SeriesCountsView fresh;
        try
        {
            fresh = await Api.GetSeriesCounts(query);
        }
        catch (HttpRequestException)
        {
            if (sequence != _sequence)
                return;
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
        _countSummary = [fresh];
        _isLoading = false;
        await InvokeAsync(StateHasChanged);

        await ResultCache.SetAsync(key, new List<SeriesCountsView> { fresh }, 1);
    }
}
