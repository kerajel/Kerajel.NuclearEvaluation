using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Models.Sandbox;
using NuclearEvaluation.Server.Interfaces.EFS;

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
    readonly SandboxSettings _settings;
    readonly ILogger<SandboxMaintenanceService> _logger;

    public SandboxMaintenanceService(
        IServiceScopeFactory scopeFactory,
        IStorageQuotaService storageQuotaService,
        IOptions<SandboxSettings> settings,
        ILogger<SandboxMaintenanceService> logger)
    {
        _scopeFactory = scopeFactory;
        _storageQuotaService = storageQuotaService;
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
            await PurgeExpiredUploadsAsync(ct);
            await ResetIfDueAsync(ct);
            _storageQuotaService.Invalidate();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sandbox maintenance sweep failed.");
        }
    }

    async Task PurgeExpiredUploadsAsync(CancellationToken ct)
    {
        DateTime cutoff = DateTime.UtcNow.AddHours(-_settings.UploadRetentionHours);

        using IServiceScope scope = _scopeFactory.CreateScope();
        NuclearEvaluationServerDbContext db = scope.ServiceProvider.GetRequiredService<NuclearEvaluationServerDbContext>();
        IEfsFileService efs = scope.ServiceProvider.GetRequiredService<IEfsFileService>();

        int removedFolders = efs.PurgeOlderThan(cutoff);

        int stemEntries = await db.StemPreviewEntry
            .Where(x => db.StemPreviewFileMetadata.Any(f => f.Id == x.FileId && f.CreatedAt < cutoff))
            .ExecuteDeleteAsync(ct);
        int stemFiles = await db.StemPreviewFileMetadata
            .Where(x => x.CreatedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        int pmiFiles = await db.Set<Kernel.Models.DataManagement.PMI.PmiReportFileMetadata>()
            .Where(x => db.PmiReport.Any(r => r.Id == x.PmiReportId && r.UploadedAt < cutoff))
            .ExecuteDeleteAsync(ct);
        int pmiReports = await db.PmiReport
            .Where(x => x.UploadedAt < cutoff)
            .ExecuteDeleteAsync(ct);

        if (removedFolders + stemEntries + stemFiles + pmiFiles + pmiReports > 0)
        {
            _logger.LogInformation(
                "Purged expired uploads: {Folders} file folders, {StemFiles} STEM files, {PmiReports} PMI reports.",
                removedFolders, stemFiles, pmiReports);
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
