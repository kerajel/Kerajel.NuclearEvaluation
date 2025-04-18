﻿using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Server.Interfaces.Data;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class SeriesCountsGrid
{
    [Inject]
    public ISeriesService SeriesService { get; set; } = null!;

    SeriesCountsView[] _countSummary = [];

    public async Task RefreshSummaryData(FetchDataCommand<SeriesView> command)
    {
        SeriesCountsView seriesCountsView = await SeriesService.GetSeriesCounts(command);

        _countSummary = [seriesCountsView];

        StateHasChanged();
    }
}