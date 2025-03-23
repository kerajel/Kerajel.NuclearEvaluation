using Microsoft.AspNetCore.Components;

namespace NuclearEvaluation.Server.Shared.Generics;

public partial class ResetGridButton
{
    [Parameter]
    public EventCallback Click { get; set; }
}
