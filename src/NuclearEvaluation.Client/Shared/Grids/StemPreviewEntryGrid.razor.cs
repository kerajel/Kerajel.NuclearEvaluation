using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class StemPreviewEntryGrid : BaseGridGeneric<StemPreviewEntryView>
{
    [Parameter]
    public Guid StemSessionId { get; set; }

    public override string EntityDisplayName => "STEM preview entry";

    protected RadzenDataGrid<StemPreviewEntryView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        if (!Visible)
        {
            return;
        }

        isLoading = true;

        await FetchData(() => Api.GetStemPreviewEntryViews(
            loadDataArgs.ToDataQuery(stemSessionId: StemSessionId)));

        isLoading = false;
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
