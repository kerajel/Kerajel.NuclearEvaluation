using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Charts;
using NuclearEvaluation.Client.Shared.Generics;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Client.Validators;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;

namespace NuclearEvaluation.Client.Pages;

public partial class ProjectCard : ComponentBase
{
    const int ApmTabIndex = 4;
    const int ParticleTabIndex = 5;

    [Parameter]
    public int Id { get; set; }

    [Parameter]
    public TabRenderMode TabRenderMode { get; set; } = TabRenderMode.Client;

    [Inject]
    ProjectViewValidator ProjectViewValidator { get; set; } = null!;

    [Inject]
    NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    INuclearEvaluationApi Api { get; set; } = null!;

    internal bool _isLoading = true;

    ProjectView _projectView = null!;

    ValidatedTextBox<ProjectView> _projectNameInputRef = null!;
    SeriesGrid _seriesGridRef = null!;

    internal bool _isEditingProjectName = false;
    internal bool _isEditingSeries = false;

    HashSet<int> _selectedSeriesIds = [];
    HashSet<int> _projectSeriesIds = [];

    DateTime? _decayCorrectionDateInput;

    ApmGrid? apmGrid;
    ParticleGrid? particleGrid;

    ProjectParticleUraniumBinCountsChart? particleBinChart;
    ProjectApmUraniumBinCountsChart? apmBinChart;

    DataQuery? _apmChartQuery;
    DataQuery? _particleChartQuery;

    protected TabManager tabManager = null!;

    DataQuery ApmChartQuery => _apmChartQuery ?? CreateProjectChartQuery();

    DataQuery ParticleChartQuery => _particleChartQuery ?? CreateProjectChartQuery();

    protected override async Task OnInitializedAsync()
    {
        DataQuery query = new() { Filter = $"Id == {Id}" };

        DataResult<ProjectView> result = await Api.GetProjectViews(query);
        ProjectView? projectView = result.Entries.SingleOrDefault();

        if (projectView == null)
        {
            NavigationManager.NavigateTo("/not-found", forceLoad: true);
            return;
        }

        tabManager = new TabManager(NavigationManager, JSRuntime, "overview")
                  .AddTab("overview", 0)
                  .AddTab("series", 1)
                  .AddTab("samples", 2)
                  .AddTab("subsamples", 3)
                  .AddTab("apm", ApmTabIndex)
                  .AddTab("particles", ParticleTabIndex)
                  .Initialize();

        _projectView = projectView;

        _decayCorrectionDateInput = _projectView.DecayCorrectionDate;

        _isLoading = false;
    }

    #region EditProjectName

    async Task OnProjectNameValidationStateChanged(bool _)
    {
        await InvokeAsync(StateHasChanged);
    }

    bool CanSaveProjectName()
    {
        return _projectNameInputRef?.IsValid ?? false;
    }

    async Task EditProjectName()
    {
        _isEditingProjectName = true;
        StateHasChanged();
        await Task.Yield();
        await _projectNameInputRef.FocusAsync();
    }

    async Task SaveProjectNameChanges()
    {
        if (_projectNameInputRef.IsReadyToCommit())
        {
            await Api.UpdateProjectField(new ProjectFieldUpdate
            {
                ProjectId = _projectView.Id,
                Field = ProjectField.Name,
                StringValue = _projectView.Name,
            });

            _projectNameInputRef.Commit();

            _isEditingProjectName = false;
        }
    }

    async Task CancelEditProjectName()
    {
        _isEditingProjectName = false;
        _projectNameInputRef.CancelValidation();
        await _projectNameInputRef.CancelChanges();
    }

    #endregion

    #region EditSeries

    async Task EditSeries()
    {
        int[] currentSeriesIds = _projectView.ProjectSeries
            .Select(x => x.SeriesId)
            .ToArray();

        _selectedSeriesIds = new HashSet<int>(currentSeriesIds);
        _projectSeriesIds = new HashSet<int>(currentSeriesIds);

        _isEditingSeries = true;

        await InvokeAsync(StateHasChanged);
    }

    async Task SaveSeriesChanges()
    {
        if (!CanSaveProjectSeries())
        {
            return;
        }

        await Api.UpdateProjectSeries(new ProjectSeriesUpdate
        {
            ProjectId = _projectView.Id,
            SeriesIds = [.. _seriesGridRef.SelectedEntryIds],
        });

        NavigationManager.NavigateTo(tabManager.Uri.AbsoluteUri, forceLoad: true);
    }

    void CancelEditSeries()
    {
        _isEditingSeries = false;
        StateHasChanged();
    }

    bool CanSaveProjectSeries()
    {
        if (_selectedSeriesIds.Count == 0)
        {
            return false;
        }

        if (_selectedSeriesIds.SetEquals(_projectSeriesIds))
        {
            return false;
        }

        return true;
    }

    async Task OnSeriesSelectionChange()
    {
        await Task.Yield();
        StateHasChanged();
    }

    #endregion

    #region DecayCorrectionDate

    async Task OnDecayCorrectionDateChange()
    {
        if (_decayCorrectionDateInput != _projectView.DecayCorrectionDate)
        {
            _projectView.DecayCorrectionDate = _decayCorrectionDateInput;

            await Api.UpdateProjectField(new ProjectFieldUpdate
            {
                ProjectId = _projectView.Id,
                Field = ProjectField.DecayCorrectionDate,
                DateValue = _decayCorrectionDateInput,
            });

            StateHasChanged();
            await Task.Yield();

            if (apmGrid != null)
            {
                await apmGrid.Refresh();
            }
            if (particleGrid != null)
            {
                await particleGrid.Refresh();
            }

            if (apmBinChart != null)
            {
                await apmBinChart.Refresh();
            }
            if (particleBinChart != null)
            {
                await particleBinChart.Refresh();
            }
        }
    }

    #endregion

    async Task OnApmQueryChanged(DataQuery query)
    {
        _apmChartQuery = ToChartQuery(query);
        await InvokeAsync(StateHasChanged);
    }

    async Task OnParticleQueryChanged(DataQuery query)
    {
        _particleChartQuery = ToChartQuery(query);
        await InvokeAsync(StateHasChanged);
    }

    DataQuery CreateProjectChartQuery()
    {
        return new DataQuery
        {
            ProjectId = _projectView.Id,
            DecayCorrected = _projectView.DecayCorrectionDate.HasValue,
        };
    }

    static DataQuery ToChartQuery(DataQuery query)
    {
        return new DataQuery
        {
            Filter = query.Filter,
            PresetFilterBox = query.PresetFilterBox,
            ProjectId = query.ProjectId,
            DecayCorrected = query.DecayCorrected,
            StemSessionId = query.StemSessionId,
            PriorityIds = query.PriorityIds is null ? null : [.. query.PriorityIds],
        };
    }

    async Task OnTabChanged(int index)
    {
        await tabManager.OnTabChanged(index);
    }

    void GoBack()
    {
        NavigationManager.NavigateTo("/evaluation");
    }
}
