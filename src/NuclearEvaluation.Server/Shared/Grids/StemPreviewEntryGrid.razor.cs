using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using System.Linq.Expressions;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class StemPreviewEntryGrid : BaseGridGeneric<StemPreviewEntryView>
{
    [Parameter]
    public Guid StemSessionId { get; set; }

    [Parameter]
    public Expression<Func<StemPreviewEntryView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    public IStemPreviewEntryService StemPreviewEntryService { get; set; } = null!;

    public override string EntityDisplayName => nameof(StemPreviewEntry);

    protected RadzenDataGrid<StemPreviewEntryView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        if (!Visible)
        {
            return;
        }

        base.isLoading = true;

        FilterDataCommand<StemPreviewEntryView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = this.TopLevelFilterExpression,
            PresetFilterBox = this.GetPresetFilterBox?.Invoke(),
        };

        FilterDataResult<StemPreviewEntryView> response = await this.StemPreviewEntryService.GetStemPreviewEntryViews(StemSessionId, command);

        await FetchData(() => StemPreviewEntryService.GetStemPreviewEntryViews(StemSessionId, command));

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