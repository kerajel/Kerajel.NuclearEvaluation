namespace NuclearEvaluation.Kernel.Models.Sandbox;

/// <summary>
/// Single-row table tracking when the anonymous sandbox was last reset to seed.
/// Persisting this in the database makes the nightly reset resilient to app-pool
/// recycling on shared hosting — the job runs whenever a reset is overdue.
/// </summary>
public class SandboxState
{
    public int Id { get; set; }

    public DateTime LastResetUtc { get; set; }
}
