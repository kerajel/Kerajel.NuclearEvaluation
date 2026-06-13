using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Plotting;

namespace NuclearEvaluation.Client.Shared.Charts;

public partial class ProjectParticleUraniumBinCountsChart
{
    [Parameter]
    public int ProjectId { get; set; }

    [Inject]
    protected INuclearEvaluationApi Api { get; set; } = null!;

    ILookup<string, BinCount> _particleUraniumBinCounts = Enumerable.Empty<(string, BinCount)>()
            .ToLookup(pair => pair.Item1, pair => pair.Item2);

    protected override async Task OnInitializedAsync()
    {
        await Refresh();
    }

    public async Task Refresh()
    {
        List<IsotopeBinCounts> data = await Api.GetProjectParticleUraniumBinCounts(ProjectId);
        _particleUraniumBinCounts = data
            .SelectMany(x => x.Bins.Select(b => (x.Isotope, Bin: b)))
            .ToLookup(x => x.Isotope, x => x.Bin);
        StateHasChanged();
    }
}
