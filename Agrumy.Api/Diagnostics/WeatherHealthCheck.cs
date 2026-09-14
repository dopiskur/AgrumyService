using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Weather;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Agrumy.Api.Diagnostics
{
    /// Passive - reads WeatherEvaluator's own last-checked timestamp for the default organization's (0) own default location instead of calling OpenWeatherMap again, so refreshing this card never burns extra forecast API quota. Representative of the install as a whole, not every organization/site individually - a per-organization breakdown belongs on that organization's own page, not a server-wide health card.
    internal sealed class WeatherHealthCheck(IServerConfigRepository serverConfigRepo, ITenantRepository tenantRepo) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            Tenant? tenant = await tenantRepo.TenantGetByIdAsync(0);
            (double Lat, double Lon)? location = tenant != null ? WeatherLocationResolver.ForTenantDefault(tenant, config) : null;
            WeatherLocationState state = location is (double lat, double lon)
                ? await tenantRepo.WeatherLocationStateGetAsync(0, lat, lon)
                : new WeatherLocationState();
            if (state.WeatherCheckedAtUtc is not DateTimeOffset checkedAt)
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
