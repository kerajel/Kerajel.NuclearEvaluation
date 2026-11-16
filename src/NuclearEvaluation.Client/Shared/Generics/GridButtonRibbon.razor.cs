using Microsoft.AspNetCore.Components;

namespace NuclearEvaluation.Client.Shared.Generics;

public partial class GridButtonRibbon
{
    [Parameter]
    public bool Visible { get; set; } = true;

    [Parameter]
    public bool Compact { get; set; } = true;

    [Parameter]
    public bool HasAssistantQuery { get; set; }

    [Parameter]
    public EventCallback ClearAssistantQuery { get; set; }

    [Parameter]
    public RenderFragment? StartContent { get; set; }

    [Parameter]
    public RenderFragment ChildContent { get; set; } = null!;

    bool ShowOnlyActions => !Compact && StartContent is null && !HasAssistantQuery;

    string CardStyle =>
        ShowOnlyActions
            ? "padding: 10px; display: flex; justify-content: flex-end; align-items: flex-end;"
            : "padding: 0.6rem 0.8rem;";
}
