using Microsoft.AspNetCore.Components;
using Radzen.Blazor;
using Radzen;
using NuclearEvaluation.Library.Commands;
using System.Linq.Expressions;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using NuclearEvaluation.Library.Models.Views;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class StemPreviewEntryGrid : BaseGrid
{
    [Parameter]
    public Guid StemSessionId { get; set; }

    [Parameter]
    public Expression<Func<StemPreviewEntryView, bool>>? TopLevelFilterExpression { get; set; }

    [Inject]
    public IStemPreviewEntryService StemPreviewEntryService { get; set; } = null!;

    public override string EntityDisplayName => nameof(StemPreviewEntry);

    protected RadzenDataGrid<StemPreviewEntryView> grid = null!;
    protected IEnumerable<StemPreviewEntryView> entries = [];

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

        FilterDataResponse<StemPreviewEntryView> response = await this.StemPreviewEntryService.GetStemPreviewEntryViews(StemSessionId, command);

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