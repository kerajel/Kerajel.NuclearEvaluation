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

public partial class ProjectCard : ComponentBase, IDisposable
{
    const int SeriesTabIndex = 1;
    const int SamplesTabIndex = 2;
    const int SubSamplesTabIndex = 3;
    const int ApmTabIndex = 4;
    const int ParticleTabIndex = 5;
    const string ApmTabName = "apm";
    const string ParticleTabName = "particles";
    const string ApmDistributionId = "apm-isotope-distribution";
    const string ParticleDistributionId = "particle-isotope-distribution";

    [Parameter]
    public int Id { get; set; }

    [Parameter]
    public TabRenderMode TabRenderMode { get; set; } = TabRenderMode.Server;

    [Inject]
    ProjectViewValidator ProjectViewValidator { get; set; } = null!;

    [Inject]
    NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    INuclearEvaluationApi Api { get; set; } = null!;

    [Inject]
    SiteAssistantWorkspace AssistantWorkspace { get; set; } = null!;

    internal bool _isLoading = true;

    ProjectView _projectView = null!;
    string? _loadError;
    int? _loadedProjectId;
    int _projectLoadSequence;
    bool _disposed;

    ValidatedTextBox<ProjectView> _projectNameInputRef = null!;
    SeriesGrid _seriesGridRef = null!;

    internal bool _isEditingProjectName = false;
    internal bool _isEditingSeries = false;
    string? _seriesUpdateError;

    HashSet<int> _selectedSeriesIds = [];
    HashSet<int> _projectSeriesIds = [];

    DateTime? _decayCorrectionDateInput;

    SeriesGrid? projectSeriesGrid;
    SampleGrid? sampleGrid;
    SubSampleGrid? subSampleGrid;
    ApmGrid? apmGrid;
    ParticleGrid? particleGrid;

    ProjectParticleUraniumBinCountsChart? particleBinChart;
    ProjectApmUraniumBinCountsChart? apmBinChart;

    DataQuery? _apmChartQuery;
    DataQuery? _particleChartQuery;
    MeasurementGridScope _apmGridScope = null!;
    MeasurementGridScope _particleGridScope = null!;
    int _apmChartQueryVersion;
    int _particleChartQueryVersion;
    bool _apmGridLoaded;
    bool _particleGridLoaded;

    protected TabManager tabManager = null!;
    ElementReference _projectTabs;
    ElementReference _apmDistribution;
    ElementReference _particleDistribution;

    protected override void OnInitialized()
    {
        tabManager = new TabManager(NavigationManager, JSRuntime, "overview")
            .AddTab("overview", 0)
            .AddTab("series", SeriesTabIndex)
            .AddTab("samples", SamplesTabIndex)
            .AddTab("subsamples", SubSamplesTabIndex)
            .AddTab(ApmTabName, ApmTabIndex)
            .AddTab(ParticleTabName, ParticleTabIndex)
            .Initialize();
    }

    protected override Task OnParametersSetAsync()
    {
        if (_loadedProjectId == Id)
            return Task.CompletedTask;
        _loadedProjectId = Id;
        _isEditingProjectName = false;
        _isEditingSeries = false;
        return LoadCurrentProject();
    }

    Task LoadCurrentProject()
    {
        _isLoading = true;
        ResetMeasurementGrid(ApmTabIndex);
        ResetMeasurementGrid(ParticleTabIndex);
        AssistantWorkspace.Remove(this);
        return LoadProjectView();
    }

    async Task LoadProjectView()
    {
        int requestedId = Id;
        int sequence = ++_projectLoadSequence;
        DataQuery query = new() { Filter = $"Id == {requestedId}", Top = 1 };
        DataResult<ProjectView> result = await Api.GetProjectViews(query);
        if (_disposed || sequence != _projectLoadSequence || requestedId != Id)
            return;

        _isLoading = false;
        _loadError = null;
        if (!result.IsSuccessful)
        {
            _loadError = "Could not load this project. Please try again.";
            return;
        }
        ProjectView? projectView = result.Entries.SingleOrDefault();
        if (projectView is null)
        {
            NavigationManager.NavigateTo("/not-found");
            return;
        }
        _projectView = projectView;
        _decayCorrectionDateInput = projectView.DecayCorrectionDate;
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
            await Api.UpdateProjectField(
                new ProjectFieldUpdate
                {
                    ProjectId = _projectView.Id,
                    Field = ProjectField.Name,
                    StringValue = _projectView.Name,
                }
            );

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
        int[] currentSeriesIds = _projectView.ProjectSeries.Select(x => x.SeriesId).ToArray();

        _selectedSeriesIds = new HashSet<int>(currentSeriesIds);
        _projectSeriesIds = new HashSet<int>(currentSeriesIds);

        _isEditingSeries = true;
        _seriesUpdateError = null;

        await InvokeAsync(StateHasChanged);
    }

    async Task SaveSeriesChanges()
    {
        if (!CanSaveProjectSeries())
        {
            return;
        }

        int[] selectedSeriesIds = [.. _seriesGridRef.SelectedEntryIds];

        try
        {
            await Api.UpdateProjectSeries(
                new ProjectSeriesUpdate
                {
                    ProjectId = _projectView.Id,
                    SeriesIds = [.. selectedSeriesIds],
                }
            );

            await LoadProjectView();

            _selectedSeriesIds = [.. selectedSeriesIds];
            _projectSeriesIds = [.. selectedSeriesIds];
            _seriesUpdateError = null;
            _isEditingSeries = false;

            await RefreshProjectDataViews();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            _seriesUpdateError = $"Could not save project series: {ex.Message}";
            await InvokeAsync(StateHasChanged);
        }
    }

    void CancelEditSeries()
    {
        _isEditingSeries = false;
        _seriesUpdateError = null;
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
            await Api.UpdateProjectField(
                new ProjectFieldUpdate
                {
                    ProjectId = _projectView.Id,
                    Field = ProjectField.DecayCorrectionDate,
                    DateValue = _decayCorrectionDateInput,
                }
            );
            _projectView.DecayCorrectionDate = _decayCorrectionDateInput;

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
        }
    }

    #endregion

    bool IsCurrentMeasurementGrid(MeasurementGridScope scope) =>
        !_disposed
        && !_isLoading
        && _loadError is null
        && Id == scope.ProjectId
        && _projectView?.Id == scope.ProjectId
        && tabManager.SelectedTabIndex == scope.TabIndex
        && ReferenceEquals(
            scope,
            scope.TabIndex == ApmTabIndex ? _apmGridScope : _particleGridScope
        );

    Task OnMeasurementLoadStarted(MeasurementGridScope scope)
    {
        if (!IsCurrentMeasurementGrid(scope))
            return Task.CompletedTask;

        if (scope.TabIndex == ApmTabIndex)
            _apmGridLoaded = false;
        else
            _particleGridLoaded = false;

        return InvokeAsync(StateHasChanged);
    }

    Task OnMeasurementQueryChanged(MeasurementGridScope scope, DataQuery query)
    {
        if (query.ProjectId != scope.ProjectId || !IsCurrentMeasurementGrid(scope))
            return Task.CompletedTask;

        if (scope.TabIndex == ApmTabIndex)
        {
            _apmChartQuery = ToChartQuery(query);
            _apmChartQueryVersion++;
            _apmGridLoaded = true;
        }
        else
        {
            _particleChartQuery = ToChartQuery(query);
            _particleChartQueryVersion++;
            _particleGridLoaded = true;
        }

        return InvokeAsync(StateHasChanged);
    }

    static DataQuery ToChartQuery(DataQuery query)
    {
        return new DataQuery
        {
            Filter = query.Filter,
            AssistantQuery = query.AssistantQuery,
            OrderBy = query.AssistantQuery?.Limit is not null ? query.OrderBy : null,
            PresetFilterBox = query.PresetFilterBox,
            ProjectId = query.ProjectId,
            DecayCorrected = query.DecayCorrected,
            StemSessionId = query.StemSessionId,
            PriorityIds = query.PriorityIds is null ? null : [.. query.PriorityIds],
        };
    }

    async Task RefreshProjectDataViews()
    {
        if (projectSeriesGrid != null)
        {
            await projectSeriesGrid.Refresh();
        }
        if (sampleGrid != null)
        {
            await sampleGrid.Refresh();
        }
        if (subSampleGrid != null)
        {
            await subSampleGrid.Refresh();
        }
        if (apmGrid != null)
        {
            await apmGrid.Refresh();
        }
        if (particleGrid != null)
        {
            await particleGrid.Refresh();
        }
    }

    Task OnTabChanged(int index)
    {
        ResetMeasurementGrid(index);
        return tabManager.OnTabChanged(index);
    }

    void ResetMeasurementGrid(int index)
    {
        if (index == ApmTabIndex)
        {
            _apmGridScope = new(Id, index);
            _apmChartQuery = null;
            _apmGridLoaded = false;
        }
        else if (index == ParticleTabIndex)
        {
            _particleGridScope = new(Id, index);
            _particleChartQuery = null;
            _particleGridLoaded = false;
        }
    }

    async Task OpenRelatedRecords(int index)
    {
        await OnTabChanged(index);
        await _projectTabs.FocusAsync();
    }

    string DistributionHref(string tab, string id) =>
        NavigationManager.ToAbsoluteUri($"projects/{Id}?tab={tab}#{id}").AbsoluteUri;

    protected override void OnAfterRender(bool firstRender)
    {
        if (_isLoading || _loadError is not null || _projectView?.Id != Id)
        {
            AssistantWorkspace.Remove(this);
            return;
        }

        int projectId = Id;
        (AssistantSurface surface, IDataGridComponent? grid) = GetAssistantGrid();
        AssistantWorkspace.Set(
            this,
            new(AssistantPage.Project, surface),
            action => ApplyAssistantAction(action, projectId, surface, grid),
            scope: (projectId, grid)
        );
    }

    (AssistantSurface Surface, IDataGridComponent? Grid) GetAssistantGrid() =>
        tabManager.SelectedTabIndex switch
        {
            SeriesTabIndex when projectSeriesGrid is not null => (
                AssistantSurface.Series,
                projectSeriesGrid
            ),
            SamplesTabIndex when sampleGrid is not null => (AssistantSurface.Samples, sampleGrid),
            SubSamplesTabIndex when subSampleGrid is not null => (
                AssistantSurface.SubSamples,
                subSampleGrid
            ),
            ApmTabIndex when apmGrid is not null => (AssistantSurface.Apms, apmGrid),
            ParticleTabIndex when particleGrid is not null => (
                AssistantSurface.Particles,
                particleGrid
            ),
            _ => (AssistantSurface.None, null),
        };

    async Task<bool> ApplyAssistantAction(
        AssistantAction action,
        int projectId,
        AssistantSurface surface,
        IDataGridComponent? grid
    )
    {
        if (
            Id != projectId
            || _isLoading
            || _loadError is not null
            || grid is null
            || GetAssistantGrid() != (surface, grid)
        )
            return false;

        bool clear = action.Kind == AssistantActionKind.ClearGrid;
        if (!clear && action.Kind != AssistantActionKind.FilterGrid)
            return false;

        if (grid is SeriesGrid series)
        {
            if (!clear && action.SeriesQuery is null)
                return false;
            await series.SetAssistantQuery(clear ? null : action.SeriesQuery);
        }
        else
        {
            if (!clear && action.GridQuery?.Grid != surface)
                return false;
            await grid.SetAssistantGridQuery(clear ? null : action.GridQuery);
        }
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        AssistantWorkspace.Remove(this);
        GC.SuppressFinalize(this);
    }

    void GoBack()
    {
        NavigationManager.NavigateTo("/evaluation");
    }

    sealed class MeasurementGridScope(int projectId, int tabIndex)
    {
        public int ProjectId { get; } = projectId;
        public int TabIndex { get; } = tabIndex;
    }
}
