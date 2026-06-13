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

    public override string EntityDisplayName => nameof(Apm);

    protected RadzenDataGrid<ApmView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        isLoading = true;

        DataQuery query = loadDataArgs.ToDataQuery(
            presetFilterBox: GetPresetFilterBox?.Invoke(),
            projectId: ProjectId,
            decayCorrected: EnableDecayCorrection);

        await FetchData(query, () => Api.GetApmViews(query));

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
