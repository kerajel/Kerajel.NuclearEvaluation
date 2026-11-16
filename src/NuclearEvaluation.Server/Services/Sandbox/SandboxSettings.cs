namespace NuclearEvaluation.Server.Services.Sandbox;

/// <summary>
/// Abuse-protection and ephemeral-data settings for the anonymous public site.
/// Bound from the "Sandbox" configuration section.
/// </summary>
public class SandboxSettings
{
    /// <summary>Storage for temporary uploads and DuckDB spill files, relative to the content root.</summary>
    public string? StorageDirectory { get; set; }

    /// <summary>When true, migrations run during startup and seed data is queued in the background.</summary>
    public bool SeedOnStartup { get; set; } = true;

    /// <summary>When true, the database is periodically reset to seed data.</summary>
    public bool ResetEnabled { get; set; } = true;

    /// <summary>How often the sandbox is reset to seed.</summary>
    public int ResetIntervalHours { get; set; } = 24;

    /// <summary>Uploaded files and staged rows older than this are purged.</summary>
    public int UploadRetentionHours { get; set; } = 24;

    /// <summary>Idle STEM preview sessions and their retained uploads are evicted after this.</summary>
    public int StemSessionIdleMinutes { get; set; } = 30;

    /// <summary>Maximum concurrent DuckDB operations across all STEM preview sessions.</summary>
    public int StemPreviewMaxConcurrentOperations { get; set; } = 2;

    /// <summary>How often the maintenance sweep runs.</summary>
    public int SweepIntervalMinutes { get; set; } = 30;

    /// <summary>Global storage ceiling; uploads are rejected once exceeded.</summary>
    public long MaxStorageBytes { get; set; } = 2L * 1024 * 1024 * 1024; // 2 GB

    /// <summary>Maximum uploads accepted per client IP per day.</summary>
    public int MaxUploadsPerIpPerDay { get; set; } = 50;

    /// <summary>General request rate limit window, in seconds.</summary>
    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>General requests permitted per IP within the window.</summary>
    public int RateLimitPermitPerWindow { get; set; } = 300;

    internal DirectoryInfo ResolveStorageDirectory(string contentRootPath) =>
        new(
            string.IsNullOrWhiteSpace(StorageDirectory)
                ? Path.Combine(
                    Directory.GetParent(contentRootPath)?.FullName ?? contentRootPath,
                    "NuclearEvaluationStorage"
                )
                : Path.GetFullPath(StorageDirectory, contentRootPath)
        );
}
