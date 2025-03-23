using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Server.Services;
using NuclearEvaluation.Server.Shared.Charts;
using NuclearEvaluation.Server.Shared.Generics;
using NuclearEvaluation.Server.Shared.Grids;
using NuclearEvaluation.Shared.Validators;
using Radzen;

namespace NuclearEvaluation.Server.Pages;

public partial class ProjectCard : ComponentBase
{
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
    IProjectService ProjectService { get; set; } = null!;

    internal bool _isLoading = true;

    ProjectView _projectView = null!;

    ValidatedTextBox<ProjectView> _projectNameInputRef = null!;
    SeriesGrid _seriesGridRef = null!;

    internal bool _isEditingProjectName = false;
    internal bool _isEditingSeries = false;

    HashSet<int> _selectedSeriesIds = [];
    HashSet<int> _projectSeriesIds = [];

    DateTime? _decayCorrectionDateInput;

    ApmGrid apmGrid = null!;
    ParticleGrid particleGrid = null!;

    ProjectParticleUraniumBinCountsChart particleBinChart = null!;
    ProjectApmUraniumBinCountsChart apmBinChart = null!;

    protected TabManager tabManager = null!;

    protected override async Task OnInitializedAsync()
    {
        FilterDataCommand<ProjectView> command = new()
        {
            TopLevelFilterExpression = x => x.Id == Id,
        };
        command.Include(x => x.ProjectSeries);

        ProjectView? projectView = (await ProjectService.GetProjectViews(command)).Entries.SingleOrDefault();

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
                  .AddTab("apm", 4)
                  .AddTab("particles", 5)
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
        return !_projectNameInputRef?.HasValidationErrors ?? false;
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
        if (await _projectNameInputRef.IsReadyToCommit())
        {
            await ProjectService.UpdatePropertyFromView(
                _projectView,
                x => x.Name,
                x => x.Name);

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

        _projectView.ProjectSeries = _seriesGridRef.SelectedEntryIds
        .Select(seriesId => new ProjectViewSeriesView
        {
            ProjectId = _projectView.Id,
            SeriesId = seriesId,
        })
        .ToList();

        await ProjectService.UpdateProjectSeriesFromView(_projectView);

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

            await ProjectService.UpdatePropertyFromView(
                _projectView,
                x => x.DecayCorrectionDate,
                x => x.DecayCorrectionDate);

            StateHasChanged();
            await Task.Yield();

            await apmGrid.Refresh();
            await particleGrid.Refresh();

            await apmBinChart.Refresh();
            await particleBinChart.Refresh();
        }
    }

    #endregion


    async Task OnTabChanged(int index)
    {
        await tabManager.OnTabChanged(index);
    }

    void GoBack()
    {
        NavigationManager.NavigateTo("/evaluation");
    }
}