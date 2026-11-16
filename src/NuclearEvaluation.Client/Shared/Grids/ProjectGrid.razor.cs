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
    protected override RadzenDataGrid<ProjectView>? AssistantGrid => grid;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        DataQuery query = loadDataArgs.ToDataQuery();
        query.AssistantQuery = AssistantQuery;

        if (!await FetchData(query, () => Api.GetProjectViews(query)))
            return;

        isLoading = false;
    }

    public override Task Reset(bool resetColumnState = true, bool resetRowState = false) =>
        ResetGrid(grid, resetColumnState, resetRowState);

    public async Task Refresh()
    {
        await grid.Reload();
    }
}
