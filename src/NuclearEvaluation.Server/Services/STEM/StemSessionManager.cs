using System.Collections.Concurrent;

namespace NuclearEvaluation.Server.Services.STEM;

public interface IStemSessionManager
{
    StemSession GetOrCreate(Guid sessionId);
    StemSession? TryGet(Guid sessionId);
    Task EvictIdleAsync(TimeSpan idleFor);
    Task EvictAsync(Guid sessionId);
}

/// <summary>
/// Owns the live <see cref="StemSession"/> objects (and therefore their kept-open connections
/// and global temp tables) for the lifetime of the process, keyed by preview session id.
/// Idle sessions are evicted by the maintenance sweep, which drops their throwaway tables.
/// </summary>
public class StemSessionManager : IStemSessionManager, IAsyncDisposable
{
    readonly ConcurrentDictionary<Guid, StemSession> _sessions = new();
    readonly string _connectionString;

    public StemSessionManager(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("NuclearEvaluationServerDbConnection")
            ?? throw new InvalidOperationException("Connection string 'NuclearEvaluationServerDbConnection' is not configured.");
    }

    public StemSession GetOrCreate(Guid sessionId)
        => _sessions.GetOrAdd(sessionId, id => new StemSession(id, _connectionString));

    public StemSession? TryGet(Guid sessionId)
        => _sessions.TryGetValue(sessionId, out StemSession? session) ? session : null;

    public async Task EvictIdleAsync(TimeSpan idleFor)
    {
        DateTime cutoff = DateTime.UtcNow - idleFor;
        foreach (KeyValuePair<Guid, StemSession> kvp in _sessions)
        {
            if (kvp.Value.LastAccessUtc < cutoff && _sessions.TryRemove(kvp.Key, out StemSession? removed))
            {
                await removed.DisposeAsync();
            }
        }
    }

    public async Task EvictAsync(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out StemSession? removed))
        {
            await removed.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (StemSession session in _sessions.Values)
        {
            await session.DisposeAsync();
        }
        _sessions.Clear();
    }
}
