using Radzen;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen.Blazor;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class PmiReportGrid : BaseGridGeneric<PmiReportView>
{
    [Inject]
    protected IPmiReportService PmiReportService { get; set; } = null!;

    [Inject]
    protected IJSRuntime jsRuntime { get; set; } = null!;

    public override string EntityDisplayName => nameof(PmiReport);

    protected RadzenDataGrid<PmiReportView> grid = null!;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        isLoading = true;
        FetchDataCommand<PmiReportView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            PresetFilterBox = GetPresetFilterBox?.Invoke()
        };
        await FetchData(() => PmiReportService.GetPmiReportViews(command));
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

    public async Task DownloadReport(PmiReportView report)
    {
        using var reportStream = Stream.Null;
        using var memoryStream = new MemoryStream();
        await reportStream.CopyToAsync(memoryStream);
        byte[] fileBytes = memoryStream.ToArray(); //TODO stream, not bytes
        string base64 = Convert.ToBase64String(fileBytes);
        string fileName = report.ReportName + ".pdf";
        await jsRuntime.InvokeVoidAsync("downloadFile", fileName, base64);
    }
}
