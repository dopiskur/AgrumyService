namespace Agrumy.Api.BackgroundWorkers
{
    public sealed class SatelliteRasterRetentionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SatelliteRasterRetentionBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromDays(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<SatelliteRasterRetentionEvaluator>().RunOnceAsync(ct);
    }
}
