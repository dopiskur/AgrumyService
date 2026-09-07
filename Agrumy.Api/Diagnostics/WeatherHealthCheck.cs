using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Agrumy.Api.Diagnostics
{
    /// Passive - reads WeatherEvaluator's own last-checked timestamp instead of calling OpenWeatherMap again, so refreshing this card never burns extra forecast API quota.
    internal sealed class WeatherHealthCheck(IServerConfigRepository serverConfigRepo) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            if (config.WeatherCheckedAtUtc is not DateTimeOffset checkedAt)
            {
                return HealthCheckResult.Degraded("Weather forecast has not been checked yet.");
            }

            int intervalMinutes = config.WeatherPollIntervalMinutes is int m and > 0 ? m : 60;
            TimeSpan age = DateTimeOffset.UtcNow - checkedAt;
            // Twice the poll interval before flagging stale - one missed tick alone shouldn't turn the card red.
            return age <= TimeSpan.FromMinutes(intervalMinutes * 2)
                ? HealthCheckResult.Healthy($"Forecast last checked {FormatAge(age)} ago.")
                : HealthCheckResult.Degraded($"Forecast stale - last checked {FormatAge(age)} ago.");
        }

        private static string FormatAge(TimeSpan age) => age.TotalHours >= 1 ? $"{age.TotalHours:0.0}h" : $"{age.TotalMinutes:0}m";
    }
}
