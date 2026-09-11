using Agrumy.Shared;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Weather;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Computes each tenant's own TenantWeatherState.WeatherRainPredicted flag; DeviceConfigBuilder combines it with each zone's own opt-in into the per-device veto. Runs once per tenant - a tenant can sit at a genuinely different physical site than the server-wide default location (Tenant.Latitude/Longitude, falling back to ServerConfig.WeatherLocationLat/Lon).
    public sealed class WeatherEvaluator(
        IServerConfigRepository serverConfigRepo, ITenantRepository tenantRepo, IWeatherForecastClient weatherClient, IOptions<AgrumySettings> settingsOptions,
        ILogger<WeatherEvaluator> logger)
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(settings.WeatherApiKey))
            {
                return; // not configured (Weather:ApiKey unset) - inert, same as location below
            }

            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            IList<Tenant> tenants = await tenantRepo.TenantsGetAllAsync();
            foreach (Tenant tenant in tenants)
            {
                ct.ThrowIfCancellationRequested();
                await RunForTenantAsync(tenant, config, ct);
            }
        }

        private async Task RunForTenantAsync(Tenant tenant, ServerConfig config, CancellationToken ct)
        {
            int tenantId = tenant.IDTenant!.Value;
            double? lat = tenant.Latitude ?? config.WeatherLocationLat;
            double? lon = tenant.Longitude ?? config.WeatherLocationLon;
            if (lat is not double latitude || lon is not double longitude)
            {
                return; // null = neither this tenant nor the server-wide default has a location set yet
            }

            TenantWeatherState state = await tenantRepo.TenantWeatherStateGetAsync(tenantId);
            // WeatherBackgroundService ticks on a fixed 1-minute cadence and re-reads the current value here every tick, so an admin's edit takes effect without a restart.
            int pollMinutes = Math.Max(1, config.WeatherPollIntervalMinutes ?? settings.WeatherPollIntervalMinutes);
            if (state.WeatherCheckedAtUtc is DateTimeOffset lastChecked && DateTimeOffset.UtcNow - lastChecked < TimeSpan.FromMinutes(pollMinutes))
            {
                return; // not due yet
            }

            double? maxRainPercent = await weatherClient.GetMaxRainProbabilityPercentAsync(latitude, longitude, settings.WeatherApiKey!, ct);
            if (maxRainPercent is not double pop)
            {
                return; // fetch failed (already logged in the client) - leave the last good reading in place
            }

            double threshold = config.WeatherRainSkipThreshold ?? settings.WeatherRainSkipThreshold;
            bool rainPredicted = pop >= threshold;
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Weather check (tenant {TenantId}): max rain probability {Pop}% (threshold {Threshold}%) -> RainPredicted={RainPredicted}.", tenantId, pop, threshold, rainPredicted);
            }
            await tenantRepo.TenantWeatherStateSetWeatherAsync(tenantId, rainPredicted, DateTimeOffset.UtcNow);
        }
    }
}
