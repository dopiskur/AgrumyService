using Agrumy.Shared;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Weather;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Computes each organization's own WeatherLocationState.WeatherRainPredicted flag (DeviceConfigBuilder combines it with each zone's own opt-in into the per-device veto) and its live Outdoor* readings (RuleConditionEvaluator's SensorMetric.OutdoorTemperature/OutdoorHumidity/OutdoorWind/OutdoorPressure). Runs once per distinct real-world location a tenant's greenhouse Units/Farms or Open-Field parcels/zones resolve to (TenantWeatherLocations, WeatherLocationResolver) - two sites under the same organization no longer share one forecast just because they share a TenantID.
    public sealed class WeatherEvaluator(
        IServerConfigRepository serverConfigRepo, ITenantRepository tenantRepo, IDeviceFarmUnitRepository unitRepo, IFarmParcelRepository farmParcelRepo,
        ISimulationRepository simulationRepo, IWeatherForecastClient weatherClient, IOptions<AgrumySettings> settingsOptions, ILogger<WeatherEvaluator> logger)
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            string? apiKey = config.WeatherApiKey ?? settings.WeatherApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return; // not configured (neither Server Settings nor appsettings.json's Weather:ApiKey) - inert, same as location below
            }

            IList<Tenant> tenants = await tenantRepo.TenantsGetAllAsync();
            foreach (Tenant tenant in tenants)
            {
                ct.ThrowIfCancellationRequested();
                await RunForTenantAsync(tenant, config, apiKey, ct);
            }
        }

        private async Task RunForTenantAsync(Tenant tenant, ServerConfig config, string apiKey, CancellationToken ct)
        {
            int tenantId = tenant.IDTenant!.Value;
            IReadOnlyList<(double Lat, double Lon)> locations = await TenantWeatherLocations.ResolveDistinctAsync(unitRepo, farmParcelRepo, simulationRepo, tenantId, tenant, config);
            foreach ((double latitude, double longitude) in locations)
            {
                ct.ThrowIfCancellationRequested();
                await RunForLocationAsync(tenantId, latitude, longitude, config, apiKey, ct);
            }
        }

        private async Task RunForLocationAsync(int tenantId, double latitude, double longitude, ServerConfig config, string apiKey, CancellationToken ct)
        {
            WeatherLocationState state = await tenantRepo.WeatherLocationStateGetAsync(tenantId, latitude, longitude);
            // WeatherBackgroundService ticks on a fixed 1-minute cadence and re-reads the current value here every tick, so an admin's edit takes effect without a restart.
            int pollMinutes = Math.Max(1, config.WeatherPollIntervalMinutes ?? settings.WeatherPollIntervalMinutes);
            if (state.WeatherCheckedAtUtc is DateTimeOffset lastChecked && DateTimeOffset.UtcNow - lastChecked < TimeSpan.FromMinutes(pollMinutes))
            {
                return; // not due yet
            }

            double? maxRainPercent = await weatherClient.GetMaxRainProbabilityPercentAsync(latitude, longitude, apiKey, ct);
            if (maxRainPercent is not double pop)
            {
                return; // fetch failed (already logged in the client) - leave the last good reading in place
            }

            double threshold = config.WeatherRainSkipThreshold ?? settings.WeatherRainSkipThreshold;
            bool rainPredicted = pop >= threshold;
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Weather check (tenant {TenantId}, {Lat},{Lon}): max rain probability {Pop}% (threshold {Threshold}%) -> RainPredicted={RainPredicted}.", tenantId, latitude, longitude, pop, threshold, rainPredicted);
            }
            await tenantRepo.WeatherLocationStateSetWeatherAsync(tenantId, latitude, longitude, rainPredicted, DateTimeOffset.UtcNow);

            // Separate forecast call, same endpoint as GetFrostForecastAsync - failure here (unlike above) doesn't block the rain-skip feature, so it never returns early.
            OutdoorConditions? outdoor = await weatherClient.GetCurrentOutdoorConditionsAsync(latitude, longitude, apiKey, ct);
            if (outdoor != null)
            {
                await tenantRepo.WeatherLocationStateSetOutdoorAsync(tenantId, latitude, longitude, outdoor.TemperatureC, outdoor.HumidityPercent, outdoor.WindSpeedMetersPerSecond, outdoor.PressureHpa, DateTimeOffset.UtcNow);
            }
        }
    }
}
