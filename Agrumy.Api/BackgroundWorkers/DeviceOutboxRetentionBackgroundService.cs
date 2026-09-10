namespace Agrumy.Api.BackgroundWorkers
{
    /// Thin PeriodicBackgroundService wrapper - the actual logic lives in DeviceOutboxRetentionEvaluator, kept separate for testability.
    public sealed class DeviceOutboxRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceOutboxRetentionBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromDays(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<DeviceOutboxRetentionEvaluator>().RunOnceAsync(ct);
    }
}
