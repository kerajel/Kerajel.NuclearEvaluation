using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class SeriesCountsGrid
{
    [Inject]
    public INuclearEvaluationApi Api { get; set; } = null!;

    SeriesCountsView[] _countSummary = [];

    public async Task RefreshSummaryData(DataQuery query)
    {
        SeriesCountsView seriesCountsView = await Api.GetSeriesCounts(query);

        _countSummary = [seriesCountsView];

        StateHasChanged();
    }
}
