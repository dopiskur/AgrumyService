namespace Agrumy.Api.BackgroundWorkers
{
    /// Daily cadence (Detaljni dizajn S, B3) - distributed lock via the shared base class means only one replica runs a tick even with multiple app instances.
    public sealed class SatelliteSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SatelliteSyncBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromDays(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<SatelliteSyncEvaluator>().RunOnceAsync(ct: ct);
    }
}
