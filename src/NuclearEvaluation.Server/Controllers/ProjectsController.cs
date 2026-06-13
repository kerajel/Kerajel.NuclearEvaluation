using Microsoft.AspNetCore.Mvc;
using NuclearEvaluation.Server.Interfaces.Evaluation;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpPost("field")]
    public async Task<IActionResult> UpdateField([FromBody] ProjectFieldUpdate update)
    {
        await _projectService.UpdateField(update);
        return Ok();
    }

    [HttpPost("series")]
    public async Task<IActionResult> UpdateSeries([FromBody] ProjectSeriesUpdate update)
    {
        await _projectService.UpdateProjectSeries(update.ProjectId, update.SeriesIds);
        return Ok();
    }

    [HttpGet("name-available")]
    public async Task<bool> NameAvailable([FromQuery] string name, [FromQuery] int excludeId = 0)
        => await _projectService.IsNameAvailable(name, excludeId);
}
