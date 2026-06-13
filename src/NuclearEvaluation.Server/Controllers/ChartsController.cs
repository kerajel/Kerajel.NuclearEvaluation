using Microsoft.AspNetCore.Mvc;
using NuclearEvaluation.Server.Interfaces.Evaluation;
using NuclearEvaluation.Shared.Models.Plotting;

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

    [HttpGet("apm-bin-counts/{projectId:int}")]
    public async Task<List<IsotopeBinCounts>> ApmBinCounts(int projectId)
        => ToList(await _chartService.GetProjectApmUraniumBinCounts(projectId));

    [HttpGet("particle-bin-counts/{projectId:int}")]
    public async Task<List<IsotopeBinCounts>> ParticleBinCounts(int projectId)
        => ToList(await _chartService.GetProjectParticleUraniumBinCounts(projectId));

    static List<IsotopeBinCounts> ToList(ILookup<string, BinCount> lookup)
        => [.. lookup.Select(g => new IsotopeBinCounts { Isotope = g.Key, Bins = [.. g] })];
}
