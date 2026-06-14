namespace NuclearEvaluation.Server.Services.Sandbox;

public interface IStorageQuotaService
{
    /// <summary>Returns true if an upload of the given size fits under the global ceiling.</summary>
    bool CanAccept(long incomingBytes);

    /// <summary>Invalidates the cached usage figure (e.g. after a purge or upload).</summary>
    void Invalidate();
}
