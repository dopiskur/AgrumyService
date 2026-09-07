namespace Agrumy.Api.Dal.Interface
{
    /// Grants at most one caller across the whole fleet a given key at a time; a held lock releases via DisposeAsync, and a missed acquisition means the caller must skip its work, not wait/retry.
    public interface IDistributedLock
    {
        Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan leaseDuration, CancellationToken ct);
    }
}
