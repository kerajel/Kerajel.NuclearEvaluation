using Microsoft.Extensions.Options;

namespace NuclearEvaluation.Server.Services.Sandbox;

/// <summary>
/// Runs the expensive seed/reset work after host startup so IIS does not time out app launch.
/// </summary>
public class StartupSeedService : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly SandboxSettings _settings;
    readonly ILogger<StartupSeedService> _logger;

    public StartupSeedService(
        IServiceScopeFactory scopeFactory,
        IOptions<SandboxSettings> settings,
        ILogger<StartupSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        if (!_settings.SeedOnStartup)
        {
            return;
        }

        await SeedWithRetryAsync(stoppingToken);
    }

    async Task SeedWithRetryAsync(CancellationToken ct)
    {
        const int maxAttempts = 10;
        for (int attempt = 1; !ct.IsCancellationRequested; attempt++)
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                IDatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

                await seeder.EnsureSeededAsync(ct);
                if (_settings.ResetEnabled)
                {
                    TimeSpan resetInterval = TimeSpan.FromHours(Math.Max(1, _settings.ResetIntervalHours));
                    await seeder.ResetToSeedIfDueAsync(resetInterval, ct);
                }

                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "Database seed not ready (attempt {Attempt}/{Max}); retrying in 5s.", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database seed failed after {MaxAttempts} attempts.", maxAttempts);
                return;
            }
        }
    }
}
