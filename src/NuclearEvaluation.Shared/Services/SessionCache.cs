using System.Collections.Concurrent;
using NuclearEvaluation.Kernel.Interfaces;

namespace NuclearEvaluation.Shared.Services;

public class SessionCache : ISessionCache
{
    private readonly ConcurrentDictionary<string, object?> _cache = new();

    public void Add<T>(string key, T? value)
    {
        _cache[key] = value;
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out object? cachedValue) && cachedValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default!;
        return false;
    }

    public T GetOrDefault<T>(string key)
    {
        if (_cache.TryGetValue(key, out object? cachedValue) && cachedValue is T typedValue)
        {
            return typedValue;
        }
        return default!;
    }
}