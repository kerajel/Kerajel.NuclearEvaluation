namespace NuclearEvaluation.Server.Services.Assistant;

internal sealed class AssistantCatalogCache<T> : IDisposable
    where T : class
{
    readonly SemaphoreSlim _gate = new(1, 1);
    T? _schema;

    public async Task<T> GetAsync(Func<CancellationToken, Task<T>> fetch, CancellationToken ct)
    {
        if (Volatile.Read(ref _schema) is { } cached)
            return cached;

        await _gate.WaitAsync(ct);
        try
        {
            if (_schema is { } refreshed)
                return refreshed;

            T schema = await fetch(ct);
            Volatile.Write(ref _schema, schema);
            return schema;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
