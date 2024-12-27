using NuclearEvaluation.Library.Models.Views;
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Interfaces;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class SeriesCountsGrid
{
    [Inject]
    public ISeriesService SeriesService { get; set; } = null!;

    SeriesCountsView[] _countSummary = [];

    public async Task RefreshSummaryData(FilterDataCommand<SeriesView> command)
    {
        SeriesCountsView seriesCountsView = await SeriesService.GetSeriesCounts(command);

        _countSummary = [seriesCountsView];

        StateHasChanged();
    }
}