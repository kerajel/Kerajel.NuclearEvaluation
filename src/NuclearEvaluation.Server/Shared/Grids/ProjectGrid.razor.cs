﻿using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Interfaces;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class ProjectGrid : BaseGrid
{
    [Inject]
    public IProjectService ProjectService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Project);

    protected RadzenDataGrid<ProjectView> grid = null!;
    protected IEnumerable<ProjectView> entries = Enumerable.Empty<ProjectView>();

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FilterDataCommand<ProjectView> command = new()
        {
            LoadDataArgs = loadDataArgs,
        };
        
        FilterDataResponse<ProjectView> response = await this.ProjectService.GetProjectViews(command);

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