using NuclearEvaluation.Shared.Enums;

namespace NuclearEvaluation.Shared.Models.Filters;

public class PresetFilterBox
{
    public Dictionary<PresetFilterEntryType, string?> Filters { get; set; } = [];

    public string? GetOrDefault(PresetFilterEntryType entryType)
    {
        _ = Filters.TryGetValue(entryType, out string? result);
        return result;
    }

    public void Set(PresetFilterEntryType entryType, string? value)
    {
        Filters[entryType] = value;
    }

    public bool IsEmpty()
    {
        return !HasFilter();
    }

    public bool HasFilter()
    {
        return Filters.Values.Any(value => !string.IsNullOrWhiteSpace(value));
    }

    public IEnumerable<(PresetFilterEntryType EntryType, string? Value)> AsEnumerable()
    {
        foreach (KeyValuePair<PresetFilterEntryType, string?> kvp in Filters)
        {
            yield return (kvp.Key, kvp.Value);
        }
    }
}
