using System.Collections.Concurrent;

namespace NuclearEvaluation.Client.Services;

/// <summary>
/// Process-lifetime cache of the last results a grid fetched, keyed by grid identity + query.
/// Lets a grid paint its previous results instantly when the user returns to a screen, while a
/// fresh fetch runs in the background — avoiding the empty-then-populate flash.
/// </summary>
public interface IGridResultCache
{
    bool TryGet<T>(string key, out List<T> entries, out int totalCount);
    void Set<T>(string key, List<T> entries, int totalCount);
}

public class GridResultCache : IGridResultCache
{
    readonly ConcurrentDictionary<string, (object Entries, int Total)> _cache = new();

    public bool TryGet<T>(string key, out List<T> entries, out int totalCount)
    {
        if (_cache.TryGetValue(key, out (object Entries, int Total) value) && value.Entries is List<T> typed)
        {
            entries = typed;
            totalCount = value.Total;
            return true;
        }
        entries = [];
        totalCount = 0;
        return false;
    }

    public void Set<T>(string key, List<T> entries, int totalCount)
    {
        _cache[key] = (entries, totalCount);
    }
}
