using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Client.Shared.Grids;

public partial class SeriesGrid : BaseGridGeneric<SeriesView>
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public int? ProjectId { get; set; }

    [Parameter]
    public HashSet<int> SelectedEntryIds { get; set; } = [];

    [Parameter]
    public EventCallback OnEntriesSelected { get; set; }

    [Parameter]
    public EventCallback OnEntriesDeselected { get; set; }

    [Parameter]
    public EventCallback<DataQuery> OnSeriesSetChanged { get; set; }

    [Inject]
    public TooltipService TooltipService { get; set; } = null!;

    [Inject]
    public DialogService DialogService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Series);

    protected RadzenDataGrid<SeriesView> grid = null!;
    protected DataQuery? currentQuery;

    readonly DataGridEditMode _editMode = DataGridEditMode.Single;
    readonly List<SeriesView> _seriesToInsert = [];
    readonly List<SeriesView> _seriesToUpdate = [];
    readonly HashSet<int> _expandingSeries = [];

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        isLoading = true;

        DataQuery query = loadDataArgs.ToDataQuery(
            presetFilterBox: GetPresetFilterBox?.Invoke(),
            projectId: ProjectId,
            priorityIds: SelectedEntryIds.Count > 0 ? SelectedEntryIds : null);

        await FetchData(query, () => Api.GetSeriesViews(query));

        currentQuery = query;

        isLoading = false;

        _ = OnSeriesSetChanged.InvokeAsync(query);
    }

    public override async Task Reset(bool resetColumnState = true, bool resetRowState = false)
    {
        foreach (RadzenDataGridColumn<SeriesView> column in grid.ColumnsCollection)
        {
            column.SetCustomFilterExpression(null);
        }
        grid.Reset(resetColumnState, resetRowState);
        await grid.Reload();
    }

    public async Task Refresh()
    {
        await grid.Reload();
    }

    protected void ResetPendingSeries()
    {
        _seriesToInsert.Clear();
        _seriesToUpdate.Clear();
    }

    protected void ResetPendingSeries(SeriesView series)
    {
        _seriesToInsert.Remove(series);
        _seriesToUpdate.Remove(series);
    }

    protected async Task InsertRow()
    {
        if (_editMode == DataGridEditMode.Single)
        {
            ResetPendingSeries();
        }

        SeriesView seriesView = new()
        {
            SeriesType = SeriesType.Regular,
            CreatedAt = DateTime.UtcNow,
        };

        _seriesToInsert.Add(seriesView);
        await grid.InsertRow(seriesView);
    }

    protected async Task OnCreateRow(SeriesView seriesView)
    {
        int id = await Api.CreateSeries(seriesView);
        seriesView.Id = id;
        _seriesToInsert.Remove(seriesView);
        entries.Add(seriesView);
    }

    protected async Task OnUpdateRow(SeriesView seriesView)
    {
        ResetPendingSeries(seriesView);
        await Api.UpdateSeries(seriesView);
    }

    protected async Task OnExpandRow(SeriesView seriesView)
    {
        _expandingSeries.Add(seriesView.Id);
        await InvokeAsync(StateHasChanged);

        try
        {
            seriesView.Samples = await Api.GetSamplesForSeries(seriesView.Id);
        }
        finally
        {
            _expandingSeries.Remove(seriesView.Id);
        }

        await InvokeAsync(StateHasChanged);
    }

    protected void RowRender(RowRenderEventArgs<SeriesView> args)
    {
        args.Expandable = args.Data.SampleCount > 0;
    }

    protected async Task EditRow(SeriesView seriesView)
    {
        if (_editMode == DataGridEditMode.Single && _seriesToInsert.Count > 0)
        {
            ResetPendingSeries();
        }

        _seriesToUpdate.Add(seriesView);
        await grid.EditRow(seriesView);
    }

    protected async Task DeleteRow(SeriesView seriesView)
    {
        if (!CanDeleteSeries(seriesView))
        {
            return;
        }

        ConfirmOptions options = new()
        {
            OkButtonText = "Yes",
            CancelButtonText = "No",
        };
        bool? isConfirmed = await DialogService.Confirm("Are you sure you want to delete this series?", "Confirm Deletion", options);
        if (!isConfirmed.HasValue || !isConfirmed.Value)
        {
            return;
        }

        ResetPendingSeries(seriesView);

        if (entries.Contains(seriesView))
        {
            await Api.DeleteSeries([seriesView.Id]);
        }
        else
        {
            grid.CancelEditRow(seriesView);
        }
        await grid.Reload();
    }

    protected bool CanDeleteSeries(SeriesView seriesView)
    {
        return seriesView.SampleCount == 0;
    }

    protected async Task SaveRow(SeriesView seriesView)
    {
        await grid.UpdateRow(seriesView);
        _ = OnSeriesSetChanged.InvokeAsync(currentQuery);
    }

    protected async Task CancelEdit(SeriesView seriesView)
    {
        ResetPendingSeries(seriesView);
        grid.CancelEditRow(seriesView);
        await grid.Reload();
    }
}
