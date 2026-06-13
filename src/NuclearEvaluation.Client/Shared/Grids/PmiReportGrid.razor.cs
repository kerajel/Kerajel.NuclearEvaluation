using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class PmiReportGrid : BaseGridGeneric<PmiReportView>
{
    [Inject]
    protected IJSRuntime jsRuntime { get; set; } = null!;

    public override string EntityDisplayName => "PMI report";

    protected RadzenDataGrid<PmiReportView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        isLoading = true;

        await FetchData(() => Api.GetPmiReportViews(loadDataArgs.ToDataQuery()));

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

    protected async Task TriggerDownload(Guid reportId)
    {
        string downloadUrl = Api.GetPmiReportDownloadUrl(reportId);
        await jsRuntime.InvokeVoidAsync("checkAndDownloadFile", downloadUrl);
    }
}
