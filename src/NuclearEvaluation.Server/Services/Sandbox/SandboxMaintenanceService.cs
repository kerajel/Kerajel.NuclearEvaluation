using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Models.Sandbox;
using NuclearEvaluation.Server.Interfaces.EFS;
using NuclearEvaluation.Server.Services.STEM;

namespace NuclearEvaluation.Server.Services.Sandbox;

/// <summary>
/// Keeps the anonymous sandbox tidy: purges expired uploads on every sweep and, when due,
/// resets the database to seed. Reset cadence is tracked in the database so it survives
/// app-pool recycles on shared hosting.
/// </summary>
public class SandboxMaintenanceService : BackgroundService
{
    readonly IServiceScopeFactory _scopeFactory;
    readonly IStorageQuotaService _storageQuotaService;
    readonly IStemSessionManager _stemSessionManager;
    readonly SandboxSettings _settings;
    readonly ILogger<SandboxMaintenanceService> _logger;

    public SandboxMaintenanceService(
        IServiceScopeFactory scopeFactory,
        IStorageQuotaService storageQuotaService,
        IStemSessionManager stemSessionManager,
        IOptions<SandboxSettings> settings,
        ILogger<SandboxMaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _storageQuotaService = storageQuotaService;
        _stemSessionManager = stemSessionManager;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(Math.Max(1, _settings.SweepIntervalMinutes));
        using PeriodicTimer timer = new(interval);

        // Run once shortly after startup, then on the timer.
        await RunSweepSafelyAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSweepSafelyAsync(stoppingToken);
        }
    }

    async Task RunSweepSafelyAsync(CancellationToken ct)
    {
        try
        {
            PurgeExpiredFiles();
            await _stemSessionManager.EvictIdleAsync(TimeSpan.FromMinutes(Math.Max(1, _settings.StemSessionIdleMinutes)));
            await ResetIfDueAsync(ct);
            _storageQuotaService.Invalidate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sandbox maintenance sweep failed.");
        }
    }

    void PurgeExpiredFiles()
    {
        DateTime cutoff = DateTime.UtcNow.AddHours(-_settings.UploadRetentionHours);

        using IServiceScope scope = _scopeFactory.CreateScope();
        IEfsFileService efs = scope.ServiceProvider.GetRequiredService<IEfsFileService>();

        int removedFolders = efs.PurgeOlderThan(cutoff);
        if (removedFolders > 0)
        {
            _logger.LogInformation("Purged {Folders} expired upload folders.", removedFolders);
        }
    }

    async Task ResetIfDueAsync(CancellationToken ct)
    {
        if (!_settings.ResetEnabled)
        {
            return;
        }

        using IServiceScope scope = _scopeFactory.CreateScope();
        NuclearEvaluationServerDbContext db = scope.ServiceProvider.GetRequiredService<NuclearEvaluationServerDbContext>();

        SandboxState? state = await db.SandboxState.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        DateTime now = DateTime.UtcNow;
        TimeSpan resetInterval = TimeSpan.FromHours(Math.Max(1, _settings.ResetIntervalHours));

        if (state is not null && now - state.LastResetUtc < resetInterval)
        {
            return;
        }

        IDatabaseSeeder seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.ResetToSeedAsync(ct);

        if (state is null)
        {
            db.SandboxState.Add(new SandboxState { LastResetUtc = now });
        }
        else
        {
            state.LastResetUtc = now;
        }
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Sandbox reset to seed completed at {Now:u}.", now);
    }
}
