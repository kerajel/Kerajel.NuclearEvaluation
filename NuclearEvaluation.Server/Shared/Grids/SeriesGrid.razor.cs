using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.Domain;
using NuclearEvaluation.Library.Models.Views;
using Radzen.Blazor;
using Radzen;
using System.Linq.Expressions;
using NuclearEvaluation.Library.Enums;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Library.Interfaces;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class SeriesGrid
    : BaseGrid
{
    [Parameter]
    public bool EnableDecayCorrection { get; set; }

    [Parameter]
    public Expression<Func<SeriesView, bool>>? TopLevelFilterExpression { get; set; }

    [Parameter]
    public HashSet<int> SelectedEntryIds { get; set; } = [];

    [Parameter]
    public EventCallback OnEntriesSelected { get; set; }

    [Parameter]
    public EventCallback OnEntriesDeselected { get; set; }

    [Parameter]
    public EventCallback<FilterDataCommand<SeriesView>> OnSeriesSetChanged { get; set; }

    [Inject]
    public ISeriesService SeriesService { get; set; } = null!;

    [Inject]
    public TooltipService TooltipService { get; set; } = null!;

    [Inject]
    public DialogService DialogService { get; set; } = null!;

    public override string EntityDisplayName => nameof(Series);

    protected RadzenDataGrid<SeriesView> grid = null!;
    protected List<SeriesView> entries = [];
    protected FilterDataCommand<SeriesView>? currentCommand;

    readonly DataGridEditMode _editMode = DataGridEditMode.Single;
    readonly List<SeriesView> _seriesToInsert = [];
    readonly List<SeriesView> _seriesToUpdate = [];

    public override async Task LoadData(LoadDataArgs loadDataArgs)
    {
        base.isLoading = true;

        FilterDataCommand<SeriesView> command = new()
        {
            LoadDataArgs = loadDataArgs,
            TopLevelFilterExpression = this.TopLevelFilterExpression,
            PresetFilterBox = this.GetPresetFilterBox?.Invoke(),
        };

        if (SelectedEntryIds.Count > 0)
        {
            command.TopLevelOrderExpression = item => SelectedEntryIds.Contains(item.Id) ? 0 : 1;
        }

        FilterDataResponse<SeriesView> response = await this.SeriesService.GetSeriesViews(command);

        entries = response.Entries.ToList();
        totalCount = response.TotalCount;

        currentCommand = command;

        base.isLoading = false;

        _ = OnSeriesSetChanged.InvokeAsync(command);
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
        Series series = await SeriesService.CreateSeriesFromView(seriesView);
        seriesView.Id = series.Id;
        _seriesToInsert.Remove(seriesView);
        entries.Add(seriesView);
    }

    protected async Task OnUpdateRow(SeriesView seriesView)
    {
        ResetPendingSeries(seriesView);
        await SeriesService.UpdateSeriesFromView(seriesView);
    }

    protected async Task OnExpandRow(SeriesView seriesView)
    {
        await SeriesService.LoadSamples(seriesView);
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
            await SeriesService.Delete(seriesView);
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
        _ = OnSeriesSetChanged.InvokeAsync(currentCommand);
    }

    protected async Task CancelEdit(SeriesView seriesView)
    {
        ResetPendingSeries(seriesView);
        grid.CancelEditRow(seriesView);
        SeriesService.ResetPendingChanges(seriesView);
    }
}