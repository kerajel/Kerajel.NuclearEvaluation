using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.DataManagement;

public partial class DataManagementTabs
{
    [Inject]
    NavigationManager Navigation { get; set; } = null!;

    protected int _currentTabIndex;

    protected override void OnInitialized()
    {
        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(
            new Uri(Navigation.Uri).Query
        );
        _currentTabIndex =
            query.TryGetValue("tab", out StringValues tab) && tab == "stem-preview" ? 1 : 0;
    }

    protected SeriesCountsGrid? _seriesCountsGrid;

    async Task OnSeriesSetChange(DataQuery query)
    {
        if (_seriesCountsGrid != null)
            await _seriesCountsGrid.RefreshSummaryData(query);
    }
}
