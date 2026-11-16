using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class SampleGrid : BaseGridGeneric<SampleView>
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public int? ProjectId { get; set; }

    public override string EntityDisplayName => nameof(Sample);

    protected RadzenDataGrid<SampleView> grid = null!;
    protected override RadzenDataGrid<SampleView>? AssistantGrid => grid;
    protected DataQuery? currentQuery;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        isLoading = true;

        DataQuery query = loadDataArgs.ToDataQuery(
            presetFilterBox: GetPresetFilterBox?.Invoke(),
            projectId: ProjectId
        );
        query.AssistantQuery = AssistantQuery;
        currentQuery = query;

        if (!await FetchData(query, () => Api.GetSampleViews(query)))
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
