using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class SubSampleGrid : BaseGridGeneric<SubSampleView>
{
    [Parameter]
    public int? ProjectId { get; set; }

    public override string EntityDisplayName => nameof(SubSample);

    protected RadzenDataGrid<SubSampleView> grid = null!;
    protected override RadzenDataGrid<SubSampleView>? AssistantGrid => grid;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        isLoading = true;

        DataQuery query = loadDataArgs.ToDataQuery(
            presetFilterBox: GetPresetFilterBox?.Invoke(),
            projectId: ProjectId
        );
        query.AssistantQuery = AssistantQuery;

        if (!await FetchData(query, () => Api.GetSubSampleViews(query)))
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
