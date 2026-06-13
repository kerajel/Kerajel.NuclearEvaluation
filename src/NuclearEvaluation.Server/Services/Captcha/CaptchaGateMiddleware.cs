namespace NuclearEvaluation.Server.Services.Captcha;

/// <summary>
/// Requires a valid proof-of-work cookie before any data API call is served. The captcha
/// endpoints themselves and all static/WASM assets stay open so the gate UI can load.
/// </summary>
public class CaptchaGateMiddleware
{
    readonly RequestDelegate _next;
    readonly ICaptchaService _captchaService;

    public CaptchaGateMiddleware(RequestDelegate next, ICaptchaService captchaService)
    {
        _next = next;
        _captchaService = captchaService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        PathString path = context.Request.Path;

        bool isProtectedApi = path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWithSegments("/api/captcha", StringComparison.OrdinalIgnoreCase);

        if (isProtectedApi)
        {
            string? token = context.Request.Cookies[CaptchaSettings.CookieName];
            if (!_captchaService.IsVerificationTokenValid(token))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "captcha_required" });
                return;
            }
        }

        await _next(context);
    }
}
