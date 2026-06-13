using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.DataManagement;

public partial class DataManagementTabs
{
    protected int _currentTabIndex = 0;
    protected SeriesCountsGrid? _seriesCountsGrid;

    async Task OnSeriesSetChange(DataQuery query)
    {
        if (_seriesCountsGrid != null)
            await _seriesCountsGrid.RefreshSummaryData(query);
    }
}
