using Agrumy.Api.BackgroundWorkers;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Agrumy.Api.Diagnostics
{
    /// Generalizes WeatherHealthCheck's "stale after 2x its own interval" pattern to every PeriodicBackgroundService at once, instead of each worker needing its own bespoke persisted timestamp and health check.
    internal sealed class BackgroundWorkersHealthCheck(IEnumerable<IHostedService> hostedServices) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            List<string> stale = hostedServices.OfType<PeriodicBackgroundService>()
                .Where(w => w.IsStale)
                .Select(w => w.GetType().Name)
                .ToList();

            return Task.FromResult(stale.Count == 0
                ? HealthCheckResult.Healthy("All periodic background workers ticked recently.")
                : HealthCheckResult.Degraded($"Stale workers (no successful tick within 2x their interval): {string.Join(", ", stale)}."));
        }
    }
}
