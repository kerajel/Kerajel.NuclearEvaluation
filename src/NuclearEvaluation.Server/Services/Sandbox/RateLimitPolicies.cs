using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace NuclearEvaluation.Server.Services.Sandbox;

public static class RateLimitPolicies
{
    /// <summary>Stricter per-IP daily cap applied to upload endpoints.</summary>
    public const string Uploads = "uploads";

    /// <summary>
    /// Configures a global per-IP fixed-window limiter plus the stricter daily upload policy.
    /// Anonymous traffic is partitioned by client IP.
    /// </summary>
    public static RateLimiterOptions Configure(RateLimiterOptions options, SandboxSettings settings)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            string key = ClientKey(context);
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.RateLimitPermitPerWindow,
                Window = TimeSpan.FromSeconds(Math.Max(1, settings.RateLimitWindowSeconds)),
                QueueLimit = 0,
            });
        });

        options.AddPolicy(Uploads, context =>
        {
            string key = ClientKey(context);
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = settings.MaxUploadsPerIpPerDay,
                Window = TimeSpan.FromDays(1),
                QueueLimit = 0,
            });
        });

        return options;
    }

    static string ClientKey(HttpContext context)
        => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
