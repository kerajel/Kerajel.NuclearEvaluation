using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NuclearEvaluation.Shared.Contracts;
using Polly.Bulkhead;

namespace NuclearEvaluation.Server.Services.STEM;

/// <summary>Logs the upload with total MVC time, including multipart binding.</summary>
public sealed class StemUploadLog(ILogger<StemUploadLog> logger) : IAsyncResourceFilter
{
    internal long? FileBytes { get; set; }
    internal Guid? FileId { get; set; }
    internal string? Rejection { get; set; }
    internal Exception? ImportError { get; set; }

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next
    )
    {
        long startedAt = Stopwatch.GetTimestamp();
        ResourceExecutedContext? executed = null;
        Exception? requestError = null;
        try
        {
            executed = await next();
            if (!executed.ExceptionHandled)
                requestError = executed.Exception;
        }
        catch (Exception error)
        {
            requestError = error;
            throw;
        }
        finally
        {
            int? status = requestError is null ? context.HttpContext.Response.StatusCode : null;
            Exception? error = requestError ?? ImportError;
            OperationOutcome? result =
                (executed?.Result as ObjectResult)?.Value as OperationOutcome;
            bool canceled = context.HttpContext.RequestAborted.IsCancellationRequested;
            string outcome = error switch
            {
                BulkheadRejectedException => "busy",
                OperationCanceledException => canceled ? "canceled" : "canceled_or_timed_out",
                not null => "failed",
                _ when Rejection is { } reason => reason,
                _ when result is { IsSuccessful: true } && status is >= 200 and < 300 =>
                    "succeeded",
                _ when result is not null => "failed",
                _ => canceled ? "canceled" : "rejected",
            };

            const string summary =
                "STEM upload {Outcome} in {MvcElapsedMs:F0} ms (HTTP {StatusCode}, {FileBytes} bytes, file {FileId})";
            double elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            if (error is null)
            {
                logger.Log(
                    outcome == "succeeded" ? LogLevel.Information : LogLevel.Warning,
                    summary,
                    outcome,
                    elapsedMs,
                    status,
                    FileBytes,
                    FileId
                );
            }
            else
            {
                // Reader exceptions can include the filename and raw cell values.
                logger.LogWarning(
                    summary + " {ErrorKind} in {ErrorSource}",
                    outcome,
                    elapsedMs,
                    status,
                    FileBytes,
                    FileId,
                    error.GetType().Name,
                    error.TargetSite?.DeclaringType?.Name
                );
            }
        }
    }
}
