using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using System.Linq.Expressions;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Shared.Services;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class SampleGrid : BaseGridGeneric<SampleView>
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public Expression<Func<SampleView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    public ISampleService SampleService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Sample);

    protected RadzenDataGrid<SampleView> grid = null!;
    protected FetchDataCommand<SampleView>? currentCommand;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FetchDataCommand<SampleView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = this.TopLevelFilterExpression,
            PresetFilterBox = this.GetPresetFilterBox?.Invoke(),
        };

        await FetchData(() => SampleService.GetSampleViews(command));

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