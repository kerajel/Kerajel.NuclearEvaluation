using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Library.Models.Domain;
using NuclearEvaluation.Library.Models.Views;
using Radzen.Blazor;
using Radzen;
using NuclearEvaluation.Library.Commands;
using System.Linq.Expressions;
using NuclearEvaluation.Library.Interfaces;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class ApmGrid : BaseGrid
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public int? ProjectId { get; set; }

    [Parameter]
    public Expression<Func<ApmView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    public IApmService ApmService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Apm);

    protected RadzenDataGrid<ApmView> grid = null!;
    protected IEnumerable<ApmView> entries = Enumerable.Empty<ApmView>();

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FilterDataCommand<ApmView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = this.TopLevelFilterExpression,
            PresetFilterBox = this.GetPresetFilterBox?.Invoke(),
        };
        command.AddArgument(FilterDataCommand.ArgKeys.EnableDecayCorrection, EnableDecayCorrection);
        command.AddArgument(FilterDataCommand.ArgKeys.ProjectId, ProjectId);

        FilterDataResponse<ApmView> response = await this.ApmService.GetApmViews(command);

        entries = response.Entries;
        totalCount = response.TotalCount;

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