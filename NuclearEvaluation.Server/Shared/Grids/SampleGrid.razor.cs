using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.Domain;
using NuclearEvaluation.Library.Models.Views;
using Radzen.Blazor;
using Radzen;
using System.Linq.Expressions;
using NuclearEvaluation.Library.Interfaces;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class SampleGrid : BaseGrid
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public Expression<Func<SampleView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    public ISampleService SampleService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Sample);

    protected RadzenDataGrid<SampleView> grid = null!;
    protected IEnumerable<SampleView> entries = Enumerable.Empty<SampleView>();
    protected FilterDataCommand<SampleView>? currentCommand;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FilterDataCommand<SampleView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = this.TopLevelFilterExpression,
            PresetFilterBox = this.GetPresetFilterBox?.Invoke(),
        };

        FilterDataResponse<SampleView> response = await this.SampleService.GetSampleViews(command);

        entries = response.Entries;
        totalCount = response.TotalCount;

        currentCommand = command;

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