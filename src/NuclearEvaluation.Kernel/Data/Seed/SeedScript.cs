using System.Reflection;
using System.Text.RegularExpressions;

namespace NuclearEvaluation.Kernel.Data.Seed;

/// <summary>Loads the embedded database setup/seed script and splits it into executable batches.</summary>
public static partial class SeedScript
{
    const string ResourceName = "NuclearEvaluation.Kernel.Data.Seed.NuclearEvaluationServerDbSetUp.sql";

    public static string Read()
    {
        Assembly assembly = typeof(SeedScript).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded seed script '{ResourceName}' was not found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Splits the script on standalone GO separators into batches that can run individually.</summary>
    public static IReadOnlyList<string> ReadBatches()
    {
        string script = Read();
        return GoSeparator()
            .Split(script)
            .Select(b => b.Trim())
            .Where(b => b.Length > 0)
            .ToList();
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}
