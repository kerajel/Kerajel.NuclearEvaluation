using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class StemPreviewEntryGrid : BaseGridGeneric<StemPreviewEntryView>, IDisposable
{
    [Parameter]
    public Guid StemSessionId { get; set; }

    [Parameter]
    public Guid? StemFileId { get; set; }

    [Parameter]
    public StemQualityCheck? StemCheck { get; set; }

    public override string EntityDisplayName => "STEM preview entry";

    protected RadzenDataGrid<StemPreviewEntryView> grid = null!;
    CancellationTokenSource? loadCancellation;
    bool disposed;

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        if (!Visible || disposed)
        {
            return;
        }

        loadCancellation?.Cancel();
        using CancellationTokenSource cancellation = new();
        loadCancellation = cancellation;
        isLoading = true;

        try
        {
            DataQuery query = loadDataArgs.ToDataQuery(stemSessionId: StemSessionId);
            query.StemFileId = StemFileId;
            query.StemCheck = StemCheck;
            DataResult<StemPreviewEntryView> result = await Api.GetStemPreviewEntryViews(
                query,
                cancellation.Token
            );
            if (loadCancellation == cancellation)
            {
                ApplyResult(result);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        finally
        {
            if (loadCancellation == cancellation)
            {
                loadCancellation = null;
                isLoading = false;
            }
        }
    }

    public void Dispose()
    {
        disposed = true;
        loadCancellation?.Cancel();
        loadCancellation = null;
    }

    public override async Task Reset(bool resetColumnState = true, bool resetRowState = false)
    {
        grid.Reset(resetColumnState, resetRowState);
        await grid.Reload();
    }

    public async Task Refresh()
    {
        if (!disposed)
        {
            await grid.Reload();
        }
    }
}
