using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.DataManagement;

public partial class DataManagementTabs : IDisposable
{
    [Inject]
    NavigationManager Navigation { get; set; } = null!;

    [Inject]
    SiteAssistantWorkspace AssistantWorkspace { get; set; } = null!;

    protected int _currentTabIndex;
    bool _startInStemQuality;
    bool _disposed;

    protected override void OnInitialized()
    {
        Dictionary<string, StringValues> query = QueryHelpers.ParseQuery(
            new Uri(Navigation.Uri).Query
        );
        _currentTabIndex =
            query.TryGetValue("tab", out StringValues tab) && tab == "stem-preview" ? 1 : 0;
        _startInStemQuality =
            _currentTabIndex == 1
            && query.TryGetValue("view", out StringValues view)
            && view == "quality";
    }

    protected SeriesCountsGrid? _seriesCountsGrid;
    SeriesGrid? _seriesGrid;
    StemPreview? _stemPreview;

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            UpdateAssistantContext();
    }

    void OnTabChanged(int index)
    {
        _currentTabIndex = index;
        UpdateAssistantContext();
    }

    void UpdateAssistantContext()
    {
        if (_disposed)
            return;

        if (_currentTabIndex == 1 && _stemPreview is { } stem)
        {
            object scope = stem.AssistantScope;
            AssistantWorkspace.Set(
                this,
                stem.AssistantContext,
                action =>
                    !_disposed && _currentTabIndex == 1 && Equals(stem.AssistantScope, scope)
                        ? stem.ApplyAssistantAction(action)
                        : Task.FromResult(false),
                scope
            );
        }
        else if (_currentTabIndex == 0)
        {
            AssistantWorkspace.Set(
                this,
                new(AssistantPage.DataManagement, AssistantSurface.Series),
                ApplySeriesAction
            );
        }
    }

    async Task<bool> ApplySeriesAction(AssistantAction action)
    {
        if (_disposed || _currentTabIndex != 0 || _seriesGrid is not { } grid)
            return false;

        switch (action.Kind)
        {
            case AssistantActionKind.FilterGrid when action.SeriesQuery is { } query:
                await grid.SetAssistantQuery(query);
                return true;
            case AssistantActionKind.ClearGrid:
                await grid.SetAssistantQuery(null);
                return true;
            default:
                return false;
        }
    }

    async Task OnSeriesSetChange(DataQuery query)
    {
        if (_seriesCountsGrid != null)
            await _seriesCountsGrid.RefreshSummaryData(query);
    }

    public void Dispose()
    {
        _disposed = true;
        AssistantWorkspace.Remove(this);
        GC.SuppressFinalize(this);
    }
}
