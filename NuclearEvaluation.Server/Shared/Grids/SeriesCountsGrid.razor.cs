using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Interfaces;

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