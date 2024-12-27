using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Library.Models.Domain;
using NuclearEvaluation.Library.Models.Views;
using Radzen.Blazor;
using Radzen;
using NuclearEvaluation.Library.Commands;
using System.Linq.Expressions;
using NuclearEvaluation.Library.Interfaces;

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