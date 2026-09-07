namespace Agrumy.Api.BackgroundWorkers
{
    /// Thin PeriodicBackgroundService wrapper - the actual logic lives in SensorDataArchiveEvaluator, kept separate for testability. Runs daily like SensorDataRetentionBackgroundService: cheap to re-check every day, and CustomRollingDays mode specifically wants a moving window re-evaluated continuously rather than once a year.
    public sealed class SensorDataArchiveBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SensorDataArchiveBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromDays(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<SensorDataArchiveEvaluator>().RunOnceAsync(ct);
    }
}
