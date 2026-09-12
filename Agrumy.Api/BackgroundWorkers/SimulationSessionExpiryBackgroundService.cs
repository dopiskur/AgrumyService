namespace Agrumy.Api.BackgroundWorkers
{
    /// Thin PeriodicBackgroundService wrapper - the actual logic lives in SimulationSessionExpiryEvaluator, kept separate for testability. A 5-minute interval, not daily like most other evaluators - the 48h cutoff is a SAFETY net (a real physical relay left stuck simulating), so it shouldn't be able to overrun by up to a day.
    public sealed class SimulationSessionExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SimulationSessionExpiryBackgroundService> logger)
        : PeriodicBackgroundService(scopeFactory, logger)
    {
        protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct) =>
            scopedProvider.GetRequiredService<SimulationSessionExpiryEvaluator>().RunOnceAsync(ct);
    }
}
