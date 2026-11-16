using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Client.Shared.Charts;

public partial class ProjectParticleUraniumBinCountsChart
{
    static readonly string[] Isotopes = [nameof(ParticleBase.U234), nameof(ParticleBase.U235)];

    [Parameter]
    public int ProjectId { get; set; }

    [Parameter]
    public DataQuery? Query { get; set; }

    [Parameter]
    public int QueryVersion { get; set; }

    [Parameter]
    public bool AwaitingGridQuery { get; set; }

    [Parameter]
    public string? Caption { get; set; }

    [Inject]
    protected INuclearEvaluationApi Api { get; set; } = null!;

    UraniumBinHistogram? _histogram;

    public Task Refresh() => _histogram?.Refresh() ?? Task.CompletedTask;
}
