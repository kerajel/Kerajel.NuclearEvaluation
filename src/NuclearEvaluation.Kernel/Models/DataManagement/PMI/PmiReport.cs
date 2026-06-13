namespace NuclearEvaluation.Kernel.Models.DataManagement.PMI;

public class PmiReport
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly CreatedDate { get; set; }

    /// <summary>Server-side upload time, used by the ephemeral retention sweep.</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public PmiReportFileMetadata PmiReportFileMetadata { get; set; } = null!;
}
