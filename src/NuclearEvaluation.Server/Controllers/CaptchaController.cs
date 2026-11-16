using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Server.Services.Captcha;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/captcha")]
public class CaptchaController : ControllerBase
{
    readonly ICaptchaService _captchaService;
    readonly CaptchaSettings _settings;
    readonly IWebHostEnvironment _environment;

    public CaptchaController(
        ICaptchaService captchaService,
        IOptions<CaptchaSettings> settings,
        IWebHostEnvironment environment
    )
    {
        _captchaService = captchaService;
        _settings = settings.Value;
        _environment = environment;
    }

    [HttpGet("challenge")]
    public CaptchaChallenge GetChallenge() => _captchaService.CreateChallenge();

    [HttpGet("status")]
    public CaptchaStatus Status()
    {
        string? token = Request.Cookies[CaptchaSettings.CookieName];
        return new CaptchaStatus
        {
            Verified =
                _environment.IsDevelopment() || _captchaService.IsVerificationTokenValid(token),
        };
    }

    [HttpPost("verify")]
    public ActionResult<CaptchaStatus> Verify([FromBody] CaptchaSolution solution)
    {
        if (!_captchaService.VerifySolution(solution))
        {
            return BadRequest(new CaptchaStatus { Verified = false });
        }

        string token = _captchaService.IssueVerificationToken();
        Response.Cookies.Append(
            CaptchaSettings.CookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                // Secure only over HTTPS so local HTTP (docker) still works; production is HTTPS.
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(_settings.VerificationTtlDays),
                IsEssential = true,
            }
        );

        return new CaptchaStatus { Verified = true };
    }
}
