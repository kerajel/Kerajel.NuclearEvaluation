using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Server.Shared.Grids;

namespace NuclearEvaluation.Server.Shared.DataManagement;

public partial class DataManagementTabs
{
    protected int _currentTabIndex = 0;
    protected SeriesCountsGrid? _seriesCountsGrid;

    async Task OnSeriesSetChange(FetchDataCommand<SeriesView> command)
    {
        if (_seriesCountsGrid != null)
            await _seriesCountsGrid.RefreshSummaryData(command);
    }
}