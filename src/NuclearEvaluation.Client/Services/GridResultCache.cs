using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace NuclearEvaluation.Client.Services;

/// <summary>
/// Caches the last results a grid fetched in the browser's localStorage, keyed by grid
/// identity + query. Because it lives in the browser (not in app memory), the cache survives
/// full page reloads, so a grid can paint its previous results instantly while a fresh fetch
/// runs in the background — avoiding the empty-then-populate flash.
/// </summary>
public interface IGridResultCache
{
    Task<GridCacheHit<T>> TryGetAsync<T>(string key);
    Task SetAsync<T>(string key, List<T> entries, int totalCount);
}

public readonly record struct GridCacheHit<T>(bool Found, List<T> Entries, int TotalCount);

public class GridResultCache : IGridResultCache
{
    const string KeyPrefix = "ne-grid-cache:";

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    readonly IJSRuntime _js;

    public GridResultCache(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<GridCacheHit<T>> TryGetAsync<T>(string key)
    {
        try
        {
            string? json = await _js.InvokeAsync<string?>("localStorage.getItem", KeyPrefix + key);
            if (string.IsNullOrEmpty(json))
            {
                return new GridCacheHit<T>(false, [], 0);
            }

            Envelope<T>? envelope = JsonSerializer.Deserialize<Envelope<T>>(json, JsonOptions);
            return envelope is null
                ? new GridCacheHit<T>(false, [], 0)
                : new GridCacheHit<T>(true, envelope.Entries, envelope.TotalCount);
        }
        catch
        {
            // Corrupt entry or storage unavailable — treat as a miss.
            return new GridCacheHit<T>(false, [], 0);
        }
    }

    public async Task SetAsync<T>(string key, List<T> entries, int totalCount)
    {
        try
        {
            string json = JsonSerializer.Serialize(new Envelope<T> { Entries = entries, TotalCount = totalCount }, JsonOptions);
            await _js.InvokeVoidAsync("localStorage.setItem", KeyPrefix + key, json);
        }
        catch
        {
            // Best-effort: ignore quota errors or storage being unavailable.
        }
    }

    sealed class Envelope<T>
    {
        public List<T> Entries { get; set; } = [];
        public int TotalCount { get; set; }
    }
}
