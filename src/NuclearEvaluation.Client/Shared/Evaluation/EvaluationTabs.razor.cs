using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Services;
using NuclearEvaluation.Client.Shared.Evaluation.QueryBuilder;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Evaluation;

public partial class EvaluationTabs : IDisposable
{
    internal const string QueryBuilderRoute = "evaluation?tab=query-builder";
    internal const string AdvancedExampleRoute =
        QueryBuilderRoute + "&example=" + QueryBuilderExample.AdvancedSlug;
    const int QueryBuilderTabIndex = 1;

    [Parameter]
    public bool OpenAdvancedExample { get; set; }

    [Inject]
    NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    SiteAssistantWorkspace AssistantWorkspace { get; set; } = null!;

    protected TabManager tabManager = null!;
    ProjectGrid? _projectGrid;
    QueryBuilderCard? _queryBuilder;
    bool _exampleRequested;
    bool _pendingExample;

    protected override void OnInitialized()
    {
        tabManager = new TabManager(NavigationManager, JSRuntime, "projects")
            .AddTab("projects", 0)
            .AddTab("query-builder", QueryBuilderTabIndex)
            .Initialize();
    }

    protected override void OnParametersSet()
    {
        if (OpenAdvancedExample && !_exampleRequested)
        {
            _pendingExample = true;
            tabManager.SelectedTabIndex = QueryBuilderTabIndex;
        }
        else if (!OpenAdvancedExample)
        {
            _pendingExample = false;
        }
        _exampleRequested = OpenAdvancedExample;
    }

    async Task OnTabChanged(int index)
    {
        Task change = tabManager.OnTabChanged(index);
        PublishAssistantContext();
        await change;
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => OnBuilderViewChangedAsync();

    async Task OnBuilderViewChangedAsync()
    {
        PublishAssistantContext();
        if (
            !_pendingExample
            || tabManager.SelectedTabIndex != QueryBuilderTabIndex
            || _queryBuilder is not { IsAssistantReady: true } builder
        )
            return;

        _pendingExample = false;
        Task<bool> apply = builder.ApplyAssistantAction(
            new(AssistantActionKind.ShowQueryBuilderExample)
        );
        NavigationManager.NavigateTo(QueryBuilderRoute, replace: true);
        await apply;
    }

    void PublishAssistantContext()
    {
        if (tabManager.SelectedTabIndex == 0 && _projectGrid is { } grid)
        {
            AssistantWorkspace.Set(
                this,
                new(AssistantPage.Evaluation, AssistantSurface.Projects),
                ApplyProjectsAction,
                grid
            );
        }
        else if (
            tabManager.SelectedTabIndex == QueryBuilderTabIndex
            && _queryBuilder is { IsAssistantReady: true } builder
        )
        {
            AssistantSurface result = builder.AssistantResultSurface;
            long revision = builder.AssistantDraftRevision;
            AssistantWorkspace.Set(
                this,
                new(AssistantPage.Evaluation, AssistantSurface.QueryBuilder, result),
                action =>
                    tabManager.SelectedTabIndex == QueryBuilderTabIndex
                    && ReferenceEquals(_queryBuilder, builder)
                    && builder.AssistantResultSurface == result
                    && builder.AssistantDraftRevision == revision
                        ? builder.ApplyAssistantAction(action)
                        : Task.FromResult(false),
                (builder, revision)
            );
        }
        else
        {
            AssistantWorkspace.Remove(this);
        }
    }

    async Task<bool> ApplyProjectsAction(AssistantAction action)
    {
        if (tabManager.SelectedTabIndex != 0 || _projectGrid is null)
            return false;
        if (action.Kind == AssistantActionKind.ClearGrid)
            await _projectGrid.SetAssistantGridQuery(null);
        else if (
            action.Kind == AssistantActionKind.FilterGrid
            && action.GridQuery is { Grid: AssistantSurface.Projects } query
        )
            await _projectGrid.SetAssistantGridQuery(query);
        else
            return false;
        return true;
    }

    public void Dispose()
    {
        AssistantWorkspace.Remove(this);
        GC.SuppressFinalize(this);
    }
}
