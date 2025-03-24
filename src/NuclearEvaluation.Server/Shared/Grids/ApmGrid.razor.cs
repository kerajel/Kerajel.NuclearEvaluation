using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using System.Linq.Expressions;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class ApmGrid : BaseGridGeneric<ApmView>
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public int? ProjectId { get; set; }

    [Parameter]
    public Expression<Func<ApmView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    protected IApmService ApmService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Apm);

    protected RadzenDataGrid<ApmView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FilterDataCommand<ApmView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = TopLevelFilterExpression,
            PresetFilterBox = GetPresetFilterBox?.Invoke(),
        };
        command.AddArgument(FilterDataCommand.ArgKeys.EnableDecayCorrection, EnableDecayCorrection);
        command.AddArgument(FilterDataCommand.ArgKeys.ProjectId, ProjectId);

        await FetchData(() => ApmService.GetApmViews(command));

        base.isLoading = false;
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