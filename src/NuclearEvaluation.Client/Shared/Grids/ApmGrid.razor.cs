using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class ApmGrid : BaseGridGeneric<ApmView>
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public int? ProjectId { get; set; }

    [Parameter]
    public EventCallback<DataQuery> OnQueryChanged { get; set; }

    [Parameter]
    public EventCallback OnLoadStarted { get; set; }

    public override string EntityDisplayName => nameof(Apm);

    protected RadzenDataGrid<ApmView> grid = null!;
    protected override RadzenDataGrid<ApmView>? AssistantGrid => grid;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        await OnLoadStarted.InvokeAsync();
        isLoading = true;

        DataQuery query = loadDataArgs.ToDataQuery(
            presetFilterBox: GetPresetFilterBox?.Invoke(),
            projectId: ProjectId,
            decayCorrected: EnableDecayCorrection
        );
        query.AssistantQuery = AssistantQuery;

        if (!await FetchData(query, () => Api.GetApmViews(query)))
            return;

        isLoading = false;

        await OnQueryChanged.InvokeAsync(query);
    }

    public override Task Reset(bool resetColumnState = true, bool resetRowState = false) =>
        ResetGrid(grid, resetColumnState, resetRowState);

    public async Task Refresh()
    {
        await grid.Reload();
    }
}
