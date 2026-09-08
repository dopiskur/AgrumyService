namespace Agrumy.Api.BackgroundWorkers
{
    /// Thin PeriodicBackgroundService wrapper - the actual logic lives in PurgeOrphanedSensorDataEvaluator, kept separate for testability.
    public sealed class PurgeOrphanedSensorDataBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<PurgeOrphanedSensorDataBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromDays(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<PurgeOrphanedSensorDataEvaluator>().RunScheduledAsync(ct);
    }
}
