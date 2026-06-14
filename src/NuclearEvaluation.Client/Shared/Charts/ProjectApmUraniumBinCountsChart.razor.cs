using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Plotting;
using System.Text.Json;

namespace NuclearEvaluation.Client.Shared.Charts;

public partial class ProjectApmUraniumBinCountsChart
{
    [Parameter]
    public int ProjectId { get; set; }

    [Parameter]
    public DataQuery? Query { get; set; }

    [Parameter]
    public string? Caption { get; set; }

    [Inject]
    protected INuclearEvaluationApi Api { get; set; } = null!;

    ILookup<string, BinCount> _apmUraniumBinCounts = Enumerable.Empty<(string, BinCount)>()
            .ToLookup(pair => pair.Item1, pair => pair.Item2);

    string? _lastQueryKey;

    protected override async Task OnParametersSetAsync()
    {
        DataQuery query = GetEffectiveQuery();
        string queryKey = JsonSerializer.Serialize(query);
        if (queryKey != _lastQueryKey)
        {
            _lastQueryKey = queryKey;
            await Load(query);
        }
    }

    public async Task Refresh()
    {
        await Load(GetEffectiveQuery());
    }

    DataQuery GetEffectiveQuery()
    {
        return Query ?? new DataQuery
        {
            ProjectId = ProjectId,
        };
    }

    async Task Load(DataQuery query)
    {
        List<IsotopeBinCounts> data = await Api.GetProjectApmUraniumBinCounts(query);
        _apmUraniumBinCounts = data
            .SelectMany(x => x.Bins.Select(b => (x.Isotope, Bin: b)))
            .ToLookup(x => x.Isotope, x => x.Bin);
        StateHasChanged();
    }
}
