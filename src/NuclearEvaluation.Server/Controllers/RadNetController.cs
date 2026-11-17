using Microsoft.AspNetCore.Mvc;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/radnet")]
public sealed class RadNetController(RadNetSnapshot snapshot) : ControllerBase
{
    [HttpGet("summary")]
    public Task<RadNetSummary> Summary(CancellationToken cancellationToken) =>
        snapshot.SummaryAsync(cancellationToken);

    [HttpGet("fields")]
    public IReadOnlyList<RadNetField> Fields() => RadNetCatalog.Fields;

    [HttpPost("query")]
    public async Task<ActionResult<RadNetQueryResult>> Query(
        [FromBody] RadNetQuery query,
        CancellationToken cancellationToken
    )
    {
        try
        {
            RadNetQueryResult? results = await snapshot.QueryAsync(query, cancellationToken);
            if (results is not null)
                return results;
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The RadNet snapshot is unavailable."
            );
        }
        catch (ArgumentException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }
    }

    [HttpGet("files/{sourceFileId:int}/quality")]
    public async Task<ActionResult<RadNetFileQuality>> FileQuality(
        int sourceFileId,
        CancellationToken cancellationToken
    )
    {
        RadNetFileQuality? quality = await snapshot.FileQualityAsync(
            sourceFileId,
            cancellationToken
        );
        if (quality is not null)
            return quality;
        return NotFound();
    }
}
