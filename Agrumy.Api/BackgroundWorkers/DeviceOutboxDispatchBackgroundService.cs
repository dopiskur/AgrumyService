namespace Agrumy.Api.BackgroundWorkers
{
    /// Thin PeriodicBackgroundService wrapper - the actual logic lives in DeviceOutboxDispatchEvaluator, kept separate for testability.
    public sealed class DeviceOutboxDispatchBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceOutboxDispatchBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<DeviceOutboxDispatchEvaluator>().RunOnceAsync(ct);
    }
}
