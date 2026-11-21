using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NuclearEvaluation.Server.Services.Captcha;

internal static class CaptchaExtensions
{
    public static IServiceCollection AddCaptcha(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath
    )
    {
        IConfigurationSection section = configuration.GetSection("Captcha");
        services
            .AddOptions<CaptchaSettings>()
            .Bind(section)
            .Validate(
                s => s.MaxNumber is >= 1 and <= 1_000_000,
                "Captcha difficulty must be between 1 and 1,000,000."
            )
            .Validate(
                s =>
                    s.ChallengeTtlMinutes is >= 1 and <= 60
                    && s.VerificationTtlDays is >= 1 and <= 365,
                "Captcha lifetimes are outside the supported range."
            )
            .ValidateOnStart();

        CaptchaSettings settings = section.Get<CaptchaSettings>() ?? new();
        services
            .AddDataProtection()
            .SetApplicationName("NuclearEvaluation.Captcha")
            .PersistKeysToFileSystem(
                new DirectoryInfo(Path.GetFullPath(settings.KeyDirectory, contentRootPath))
            );
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<ICaptchaService, CaptchaService>();
        return services;
    }
}
