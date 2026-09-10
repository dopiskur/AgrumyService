namespace Agrumy.Api.BackgroundWorkers
{
    /// Fixed 1-minute cadence, matching WeatherBackgroundService - FrostAlertEvaluator re-reads the live poll interval every tick.
    public sealed class FrostAlertBackgroundService(IServiceScopeFactory scopeFactory, ILogger<FrostAlertBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<FrostAlertEvaluator>().RunOnceAsync(ct);
    }
}
