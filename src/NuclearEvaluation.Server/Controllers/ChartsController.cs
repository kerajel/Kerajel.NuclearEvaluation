using Microsoft.AspNetCore.Mvc;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Plotting;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/charts")]
public class ChartsController : ControllerBase
{
    readonly IChartService _chartService;

    public ChartsController(IChartService chartService)
    {
        _chartService = chartService;
    }

    [HttpPost("apm-bin-counts")]
    public async Task<List<IsotopeBinCounts>> ApmBinCounts(
        [FromBody] DataQuery query,
        CancellationToken ct
    ) => ToList(await _chartService.GetProjectApmUraniumBinCounts(query.ToCommand<ApmView>(ct)));

    [HttpGet("apm-bin-counts/{projectId:int}")]
    public async Task<List<IsotopeBinCounts>> ApmBinCountsByProject(int projectId) =>
        ToList(await _chartService.GetProjectApmUraniumBinCounts(projectId));

    [HttpPost("particle-bin-counts")]
    public async Task<List<IsotopeBinCounts>> ParticleBinCounts(
        [FromBody] DataQuery query,
        CancellationToken ct
    ) =>
        ToList(
            await _chartService.GetProjectParticleUraniumBinCounts(
                query.ToCommand<ParticleView>(ct)
            )
        );

    [HttpGet("particle-bin-counts/{projectId:int}")]
    public async Task<List<IsotopeBinCounts>> ParticleBinCountsByProject(int projectId) =>
        ToList(await _chartService.GetProjectParticleUraniumBinCounts(projectId));

    static List<IsotopeBinCounts> ToList(ILookup<string, BinCount> lookup) =>
        [.. lookup.Select(g => new IsotopeBinCounts { Isotope = g.Key, Bins = [.. g] })];
}
