namespace NuclearEvaluation.Server.Services.Sandbox;

/// <summary>
/// Abuse-protection and ephemeral-data settings for the anonymous public site.
/// Bound from the "Sandbox" configuration section.
/// </summary>
public class SandboxSettings
{
    /// <summary>When true, the database is periodically reset to seed data.</summary>
    public bool ResetEnabled { get; set; } = true;

    /// <summary>How often the sandbox is reset to seed.</summary>
    public int ResetIntervalHours { get; set; } = 24;

    /// <summary>Uploaded files and staged rows older than this are purged.</summary>
    public int UploadRetentionHours { get; set; } = 24;

    /// <summary>Idle STEM preview sessions (and their throwaway temp tables) are evicted after this.</summary>
    public int StemSessionIdleMinutes { get; set; } = 30;

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
}
