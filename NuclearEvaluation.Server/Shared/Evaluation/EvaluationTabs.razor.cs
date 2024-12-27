using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Server.Services;

namespace NuclearEvaluation.Server.Shared.Evaluation;

public partial class EvaluationTabs
{
    [Inject]
    NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    IJSRuntime JSRuntime { get; set; } = null!;

    protected TabManager tabManager = null!;

    protected override void OnInitialized()
    {
        tabManager = new TabManager(NavigationManager, JSRuntime, "projects")
            .AddTab("projects", 0)
            .AddTab("query-builder", 1)
            .Initialize();
    }

    void OnTabChanged(int index)
    {
        tabManager.OnTabChanged(index);
    }
}