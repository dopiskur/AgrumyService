using Agrumy.Api.Dal.Interface;

namespace Agrumy.Api.Dal
{
    /// Single-replica deployments have no other process to race against - always grants the lock immediately.
    internal sealed class NoOpDistributedLock : IDistributedLock
    {
        private sealed class Handle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan leaseDuration, CancellationToken ct) =>
            Task.FromResult<IAsyncDisposable?>(new Handle());
    }
}
