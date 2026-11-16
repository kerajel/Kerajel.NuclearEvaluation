using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NuclearEvaluation.Server.Services.Sandbox;

namespace NuclearEvaluation.Server.Services.STEM;

public interface IStemSessionManager
{
    string StorageDirectory { get; }
    StemSession GetOrCreate(Guid sessionId);
    StemSession? TryGet(Guid sessionId);
    Task EvictIdleAsync(TimeSpan idleFor);
    Task EvictAsync(Guid sessionId);
}

/// <summary>Tracks preview sessions and their temporary storage.</summary>
public class StemSessionManager : IStemSessionManager, IAsyncDisposable
{
    readonly ConcurrentDictionary<Guid, StemSession> _sessions = new();
    readonly SemaphoreSlim _operations;
    readonly ILogger<StemSessionManager> _logger;

    public string StorageDirectory { get; }

    public StemSessionManager(
        IOptions<SandboxSettings> settings,
        IHostEnvironment environment,
        ILogger<StemSessionManager>? logger = null
    )
        : this(
            settings,
            settings.Value.ResolveStorageDirectory(environment.ContentRootPath),
            logger
        ) { }

    internal StemSessionManager(
        IOptions<SandboxSettings> settings,
        DirectoryInfo storageRoot,
        ILogger<StemSessionManager>? logger = null
    )
    {
        // Exclude this process's live sessions from the upload retention sweep.
        StorageDirectory = Path.Combine(storageRoot.FullName, $"stem-{Guid.NewGuid():N}");
        _logger = logger ?? NullLogger<StemSessionManager>.Instance;
        int concurrency = Math.Clamp(settings.Value.StemPreviewMaxConcurrentOperations, 1, 8);
        _operations = new SemaphoreSlim(concurrency, concurrency);
    }

    public StemSession GetOrCreate(Guid sessionId) =>
        _sessions.GetOrAdd(sessionId, _ => new StemSession(
            Path.Combine(StorageDirectory, Guid.NewGuid().ToString("N")),
            _operations,
            _logger
        ));

    public StemSession? TryGet(Guid sessionId) =>
        _sessions.TryGetValue(sessionId, out StemSession? session) ? session : null;

    public Task EvictIdleAsync(TimeSpan idleFor)
    {
        DateTime cutoff = DateTime.UtcNow - idleFor;
        foreach (KeyValuePair<Guid, StemSession> session in _sessions)
        {
            try
            {
                if (session.Value.TryEvictIdle(cutoff))
                    _sessions.TryRemove(session);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to remove idle STEM preview {SessionId}.",
                    session.Key
                );
            }
        }
        return Task.CompletedTask;
    }

    public async Task EvictAsync(Guid sessionId)
    {
        if (_sessions.TryRemove(sessionId, out StemSession? removed))
            await removed.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (KeyValuePair<Guid, StemSession> session in _sessions)
            {
                try
                {
                    await session.Value.DisposeAsync();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to remove STEM preview {SessionId} on shutdown.",
                        session.Key
                    );
                }
            }
            if (Directory.Exists(StorageDirectory))
                Directory.Delete(StorageDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not clear remaining STEM preview files on shutdown.");
        }
        finally
        {
            _sessions.Clear();
            _operations.Dispose();
        }
    }
}
