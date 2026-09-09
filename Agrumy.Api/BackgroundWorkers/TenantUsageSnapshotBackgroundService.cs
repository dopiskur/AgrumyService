namespace Agrumy.Api.BackgroundWorkers
{
    /// Thin PeriodicBackgroundService wrapper - the actual logic lives in TenantUsageSnapshotEvaluator, kept separate for testability.
    public sealed class TenantUsageSnapshotBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<TenantUsageSnapshotBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromHours(24);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<TenantUsageSnapshotEvaluator>().RunOnceAsync(ct);
    }
}
