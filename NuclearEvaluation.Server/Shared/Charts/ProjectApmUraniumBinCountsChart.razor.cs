using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.Plotting;

namespace NuclearEvaluation.Server.Shared.Charts;

public partial class ProjectApmUraniumBinCountsChart
{
    [Parameter]
    public int ProjectId { get; set; }

    [Inject]
    protected IChartService ChartService { get; set; } = null!;

    ILookup<string, BinCount> _apmUraniumBinCounts = Enumerable.Empty<(string, BinCount)>()
            .ToLookup(pair => pair.Item1, pair => pair.Item2);

    protected override async Task OnInitializedAsync()
    {
        await Refresh();
    }

    public async Task Refresh()
    {
        _apmUraniumBinCounts = await ChartService.GetProjectApmUraniumBinCounts(ProjectId);
        StateHasChanged();
    }
}