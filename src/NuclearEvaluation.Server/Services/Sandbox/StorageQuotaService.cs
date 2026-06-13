using Microsoft.Extensions.Options;
using NuclearEvaluation.Server.Interfaces.EFS;

namespace NuclearEvaluation.Server.Services.Sandbox;

/// <summary>
/// Enforces the global storage ceiling. The on-disk size is cached briefly so concurrent
/// uploads do not each trigger a full directory walk.
/// </summary>
public class StorageQuotaService : IStorageQuotaService
{
    static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    readonly IEfsFileService _efsFileService;
    readonly SandboxSettings _settings;
    readonly Lock _gate = new();

    long _cachedBytes;
    DateTime _cachedAtUtc = DateTime.MinValue;

    public StorageQuotaService(IEfsFileService efsFileService, IOptions<SandboxSettings> settings)
    {
        _efsFileService = efsFileService;
        _settings = settings.Value;
    }

    public bool CanAccept(long incomingBytes)
    {
        long current = GetUsage();
        return current + incomingBytes <= _settings.MaxStorageBytes;
    }

    public void Invalidate()
    {
        lock (_gate)
        {
            _cachedAtUtc = DateTime.MinValue;
        }
    }

    long GetUsage()
    {
        lock (_gate)
        {
            if (DateTime.UtcNow - _cachedAtUtc < CacheTtl)
            {
                return _cachedBytes;
            }
        }

        long bytes = _efsFileService.GetTotalSizeBytes();

        lock (_gate)
        {
            _cachedBytes = bytes;
            _cachedAtUtc = DateTime.UtcNow;
        }

        return bytes;
    }
}
