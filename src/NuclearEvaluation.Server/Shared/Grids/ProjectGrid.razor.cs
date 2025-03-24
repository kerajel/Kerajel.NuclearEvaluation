using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Shared.Services;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class ProjectGrid : BaseGridGeneric<ProjectView>
{
    [Inject]
    public IProjectService ProjectService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Project);

    protected RadzenDataGrid<ProjectView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FilterDataCommand<ProjectView> command = new()
        {
            LoadDataArgs = loadDataArgs,
        };
        
        FilterDataResult<ProjectView> response = await this.ProjectService.GetProjectViews(command);

        await FetchData(() => ProjectService.GetProjectViews(command));

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