using System.Linq.Dynamic.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace NuclearEvaluation.Server.Controllers;

/// <summary>Keep unexpected API failures out of the SPA fallback and hide server internals.</summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<ApiExceptionHandler> logger
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct
    )
    {
        int status = exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            ArgumentException or ParseException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };
        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "API request {Method} {Path} failed.",
                context.Request.Method,
                context.Request.Path
            );
        }
        context.Response.StatusCode = status;
        await problemDetails.WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = status,
                    Title = status switch
                    {
                        400 => "The request contains an invalid value or filter.",
                        404 => "The requested record no longer exists.",
                        _ => "The request could not be completed. Please try again.",
                    },
                },
            }
        );
        return true;
    }
}
