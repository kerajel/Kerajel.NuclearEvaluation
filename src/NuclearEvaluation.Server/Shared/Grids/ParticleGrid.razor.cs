using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using System.Linq.Expressions;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Shared.Services;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class ParticleGrid : BaseGridGeneric<ParticleView>
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public int? ProjectId { get; set; }

    [Parameter]
    public Expression<Func<ParticleView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    public IParticleService ParticleService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Particle);

    protected RadzenDataGrid<ParticleView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FetchDataCommand<ParticleView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = this.TopLevelFilterExpression,
            PresetFilterBox = this.GetPresetFilterBox?.Invoke(),
        };
        command.AddArgument(FilterDataCommand.ArgKeys.EnableDecayCorrection, EnableDecayCorrection);
        command.AddArgument(FilterDataCommand.ArgKeys.ProjectId, ProjectId);

        FetchDataResult<ParticleView> response = await this.ParticleService.GetParticleViews(command);

        await FetchData(() => ParticleService.GetParticleViews(command));

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