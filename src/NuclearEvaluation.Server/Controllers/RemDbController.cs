using Microsoft.AspNetCore.Mvc;
using NuclearEvaluation.Server.Services.RemDb;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/remdb")]
public sealed class RemDbController(RemDbSnapshot snapshot) : ControllerBase
{
    [HttpGet("summary")]
    public Task<RemDbSummary> Summary(CancellationToken cancellationToken) =>
        snapshot.SummaryAsync(cancellationToken);

    [HttpGet("fields")]
    public IReadOnlyList<RemDbField> Fields() => RemDbCatalog.Fields;

    [HttpPost("query")]
    public async Task<ActionResult<RemDbQueryResult>> Query(
        [FromBody] RemDbQuery query,
        CancellationToken cancellationToken
    )
    {
        try
        {
            RemDbQueryResult? result = await snapshot.QueryAsync(query, cancellationToken);
            if (result is not null)
                return result;
            return Unavailable();
        }
        catch (ArgumentException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }
    }

    [HttpGet("files")]
    public async Task<ActionResult<RemDbFilePage>> Files(
        CancellationToken cancellationToken,
        int skip = 0,
        int top = 50
    )
    {
        try
        {
            RemDbFilePage? result = await snapshot.FilesAsync(skip, top, cancellationToken);
            if (result is not null)
                return result;
            return Unavailable();
        }
        catch (ArgumentException exception)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: exception.Message);
        }
    }

    [HttpGet("files/{sourceFileId:int}/quality")]
    public async Task<ActionResult<RemDbFileQuality>> FileQuality(
        int sourceFileId,
        CancellationToken cancellationToken
    )
    {
        RemDbFileQuality? quality = await snapshot.FileQualityAsync(
            sourceFileId,
            cancellationToken
        );
        if (quality is not null)
            return quality;
        if (!snapshot.IsAvailable)
            return Unavailable();
        return NotFound();
    }

    ObjectResult Unavailable() =>
        Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The REMdb snapshot is unavailable."
        );
}
