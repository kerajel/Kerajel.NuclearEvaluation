namespace NuclearEvaluation.Server.Services.Files;

public static class SafeFileName
{
    const int MaxFileNameLength = 255;

    public static string FromClientFileName(string? fileName, Guid fallbackId)
    {
        string normalized = (fileName ?? string.Empty).Replace('\\', '/');
        string safe = Path.GetFileName(normalized).Trim();

        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
        {
            return $"{fallbackId:N}.upload";
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = safe.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsControl(chars[i]) || chars[i] is '/' or '\\' || invalid.Contains(chars[i]))
            {
                chars[i] = '_';
            }
        }

        safe = new string(chars).Trim();
        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
        {
            return $"{fallbackId:N}.upload";
        }

        if (safe.Length <= MaxFileNameLength)
        {
            return safe;
        }

        string extension = Path.GetExtension(safe);
        if (extension.Length >= MaxFileNameLength)
        {
            return safe[..MaxFileNameLength];
        }

        int stemLength = MaxFileNameLength - extension.Length;
        return safe[..stemLength] + extension;
    }
}
