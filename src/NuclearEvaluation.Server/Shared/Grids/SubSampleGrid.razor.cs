﻿using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using System.Linq.Expressions;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Server.Interfaces.Data;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class SubSampleGrid : BaseGridGeneric<SubSampleView>
{
    [Parameter]
    public Expression<Func<SubSampleView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    public ISubSampleService SubSampleService { get; set; } = null!;

    public override string EntityDisplayName => nameof(SubSample);

    protected RadzenDataGrid<SubSampleView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FetchDataCommand<SubSampleView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = this.TopLevelFilterExpression,
            PresetFilterBox = this.GetPresetFilterBox?.Invoke(),
        };

        FetchDataResult<SubSampleView> response = await this.SubSampleService.GetSubSampleViews(command);

        await FetchData(() => SubSampleService.GetSubSampleViews(command));

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