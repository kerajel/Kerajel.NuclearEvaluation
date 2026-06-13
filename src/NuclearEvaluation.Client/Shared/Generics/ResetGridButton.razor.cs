using Microsoft.AspNetCore.Components;

namespace NuclearEvaluation.Client.Shared.Generics;

public partial class ResetGridButton
{
    [Parameter]
    public EventCallback Click { get; set; }
}
