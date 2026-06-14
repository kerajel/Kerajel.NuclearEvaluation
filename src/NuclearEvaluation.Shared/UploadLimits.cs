namespace NuclearEvaluation.Shared;

/// <summary>
/// Hard upload cap shared by the WASM client (for early UX rejection) and the API
/// (for authoritative enforcement). The site is anonymous, so this also bounds a single
/// abusive upload; the global storage ceiling and rate limits cap the aggregate.
/// </summary>
public static class UploadLimits
{
    // Generous enough for the bundled STEM preview samples (the 1,048,567-row file is ~52 MB),
    // which is the whole point of the streaming + throwaway-temp-table showcase.
    public const long MaxStemPreviewFileSizeBytes = 64L * 1024 * 1024; // 64 MB
}
