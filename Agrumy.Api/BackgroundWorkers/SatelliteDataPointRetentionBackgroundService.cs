namespace Agrumy.Api.BackgroundWorkers
{
    public sealed class SatelliteDataPointRetentionBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SatelliteDataPointRetentionBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromDays(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<SatelliteDataPointRetentionEvaluator>().RunOnceAsync(ct);
    }
}
