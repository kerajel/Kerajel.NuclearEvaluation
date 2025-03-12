using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using System.Linq.Expressions;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Interfaces;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class SubSampleGrid : BaseGrid
{
    [Parameter]
    public Expression<Func<SubSampleView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    public ISubSampleService SubSampleService { get; set; } = null!;

    public override string EntityDisplayName => nameof(SubSample);

    protected RadzenDataGrid<SubSampleView> grid = null!;
    protected IEnumerable<SubSampleView> entries = Enumerable.Empty<SubSampleView>();

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FilterDataCommand<SubSampleView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = this.TopLevelFilterExpression,
            PresetFilterBox = this.GetPresetFilterBox?.Invoke(),
        };

        FilterDataResponse<SubSampleView> response = await this.SubSampleService.GetSubSampleViews(command);

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