using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class ProjectGrid : BaseGridGeneric<ProjectView>
{
    public override string EntityDisplayName => nameof(Project);

    protected RadzenDataGrid<ProjectView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        DataQuery query = loadDataArgs.ToDataQuery();

        await FetchData(query, () => Api.GetProjectViews(query));

        isLoading = false;
    }

    public override async Task Reset(bool resetColumnState = true, bool resetRowState = false)
    {
        grid.Reset(resetColumnState, resetRowState);
        await grid.Reload();
    }

    public async Task Refresh()
    {
        await grid.Reload();
    }
}
