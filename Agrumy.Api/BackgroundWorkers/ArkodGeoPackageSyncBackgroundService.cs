namespace Agrumy.Api.BackgroundWorkers
{
    /// Daily check (upstream itself only refreshes weekly, so most ticks are a cheap HEAD-only no-op) - see ArkodGeoPackageSyncEvaluator for the actual download-if-changed logic.
    public sealed class ArkodGeoPackageSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ArkodGeoPackageSyncBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromDays(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<Agrumy.Api.Arkod.ArkodGeoPackageSyncEvaluator>().RunOnceAsync(ct);
    }
}
