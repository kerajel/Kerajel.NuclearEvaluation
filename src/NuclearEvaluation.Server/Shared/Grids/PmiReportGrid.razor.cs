using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Kernel.Models.Views;
using Radzen.Blazor;
using Radzen;

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
        command.Include(x => x.DistributionEntries);
        command.Include(x => x.FileMetadata);

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

    protected async Task TriggerDownload(Guid reportId)
    {
        string downloadUrl = $"/DownloadPmiReport?id={reportId}";
        await jsRuntime.InvokeVoidAsync("checkAndDownloadFile", downloadUrl);
    }

    private string GetDistributionTooltip(IEnumerable<PmiReportDistributionEntryView> entries)
    {
        if (entries == null || !entries.Any())
        {
            return "No distribution entries";
        }

        return string.Join(Environment.NewLine, entries.Select(entry => $"{entry.DistributionChannel}: {entry.DistributionStatus}"));
    }
}
