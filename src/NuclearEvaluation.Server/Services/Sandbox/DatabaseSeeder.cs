using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Data.Seed;
using NuclearEvaluation.Kernel.Models.Sandbox;

namespace NuclearEvaluation.Server.Services.Sandbox;

public interface IDatabaseSeeder
{
    /// <summary>Applies migrations and seeds the database if it has no domain data yet.</summary>
    Task EnsureCreatedAndSeededAsync(CancellationToken ct = default);

    /// <summary>Re-runs the idempotent seed script, restoring the sandbox to its baseline.</summary>
    Task ResetToSeedAsync(CancellationToken ct = default);

    /// <summary>Re-runs the seed script when the tracked sandbox reset time is overdue.</summary>
    Task<bool> ResetToSeedIfDueAsync(TimeSpan resetInterval, CancellationToken ct = default);
}

public class DatabaseSeeder : IDatabaseSeeder
{
    readonly NuclearEvaluationServerDbContext _dbContext;
    readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(NuclearEvaluationServerDbContext dbContext, ILogger<DatabaseSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task EnsureCreatedAndSeededAsync(CancellationToken ct = default)
    {
        await _dbContext.Database.MigrateAsync(ct);

        bool alreadySeeded = await _dbContext.Series.AnyAsync(ct);
        if (alreadySeeded)
        {
            _logger.LogInformation("Database already contains data; skipping initial seed.");
            return;
        }

        _logger.LogInformation("Seeding database for the first time.");
        await RunSeedBatchesAsync(ct);

        if (!await _dbContext.SandboxState.AnyAsync(ct))
        {
            _dbContext.SandboxState.Add(new SandboxState { LastResetUtc = DateTime.UtcNow });
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    public async Task ResetToSeedAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Resetting sandbox database to seed.");
        await RunSeedBatchesAsync(ct);
    }

    public async Task<bool> ResetToSeedIfDueAsync(TimeSpan resetInterval, CancellationToken ct = default)
    {
        SandboxState? state = await _dbContext.SandboxState.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        DateTime now = DateTime.UtcNow;

        if (state is not null && now - state.LastResetUtc < resetInterval)
        {
            return false;
        }

        await ResetToSeedAsync(ct);

        DateTime completedAt = DateTime.UtcNow;
        if (state is null)
        {
            _dbContext.SandboxState.Add(new SandboxState { LastResetUtc = completedAt });
        }
        else
        {
            state.LastResetUtc = completedAt;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Sandbox reset to seed completed at {CompletedAt:u}.", completedAt);
        return true;
    }

    async Task RunSeedBatchesAsync(CancellationToken ct)
    {
        IReadOnlyList<string> batches = SeedScript.ReadBatches();

        DbConnection connection = _dbContext.Database.GetDbConnection();
        bool openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            // One open connection across all batches so the script's #temp tables survive GO boundaries.
            foreach (string batch in batches)
            {
                using DbCommand command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = (int)TimeSpan.FromMinutes(10).TotalSeconds;
                await command.ExecuteNonQueryAsync(ct);
            }
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
