namespace NuclearEvaluation.Shared;

/// <summary>
/// Hard upload caps shared by the WASM client (for early UX rejection) and the API
/// (for authoritative enforcement). The site is anonymous, so these also act as the
/// per-request ceiling against abusive uploads.
/// </summary>
public static class UploadLimits
{
    public const long MaxStemPreviewFileSizeBytes = 20L * 1024 * 1024; // 20 MB

    public const long MaxPmiReportFileSizeBytes = 20L * 1024 * 1024; // 20 MB

    public const string PmiReportExtension = ".docx";
}
