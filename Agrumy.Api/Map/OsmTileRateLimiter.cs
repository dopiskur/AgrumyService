namespace Agrumy.Api.Map
{
    /// Server-wide outbound throttle toward tile.openstreetmap.org - OSMF's Tile Usage Policy caps a single source at ~2 req/s, and this server (not each browser) is now that one source. Singleton, so every concurrent request across every tenant/tab/device shares the same gate - unlike Program.cs's AddRateLimiter, which throttles INCOMING requests per client, this throttles the one OUTGOING path to OSM regardless of how many callers are waiting on it.
    public sealed class OsmTileRateLimiter
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private DateTime lastRequestUtc = DateTime.MinValue;
        private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(500); // ~2 req/s ceiling

        public async Task WaitAsync(CancellationToken ct = default)
        {
            await gate.WaitAsync(ct);
            try
            {
                TimeSpan sinceLast = DateTime.UtcNow - lastRequestUtc;
                if (sinceLast < MinInterval)
                {
                    await Task.Delay(MinInterval - sinceLast, ct);
                }
                lastRequestUtc = DateTime.UtcNow;
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
