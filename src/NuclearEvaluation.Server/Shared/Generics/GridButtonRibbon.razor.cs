using Microsoft.AspNetCore.Components;

namespace NuclearEvaluation.Server.Shared.Generics;

public partial class GridButtonRibbon
{
    [Parameter]
    public bool Visible { get; set; } = true;

    [Parameter]
    public string Style { get; set; } = "padding: 10px; display: flex; justify-content: flex-end; align-items: flex-end;";

    [Parameter]
    public RenderFragment ChildContent { get; set; } = null!;
}
