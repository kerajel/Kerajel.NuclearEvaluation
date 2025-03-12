using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Kernel.Models.Filters;

public class PresetFilterBox
{
    readonly Dictionary<PresetFilterEntryType, string?> _filters = [];

    public string? GetOrDefault(PresetFilterEntryType entryType)
    {
        _ = _filters.TryGetValue(entryType, out string? result);
        return result;
    }

    public void Set(PresetFilterEntryType entryType, string? value)
    {
        _filters[entryType] = value;
    }

    public bool IsEmpty()
    {
        return _filters.Count == 0;
    }

    public bool HasFilter()
    {
        return _filters.Count > 0;
    }

    public IEnumerable<(PresetFilterEntryType EntryType, string? Value)> AsEnumerable()
    {
        foreach (KeyValuePair<PresetFilterEntryType, string?> kvp in _filters)
        {
            yield return (kvp.Key, kvp.Value);
        }
    }
}