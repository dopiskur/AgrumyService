using Agrumy.Shared;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;
using Agrumy.Api.Weather;
using Agrumy.Api.Notifications;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Forecast-based early warning (temperature + cloudiness + wind - the three factors radiative frost actually depends on) fires hours before frost sets in, refined with a same-tick local DewPoint-spread reading where sensors report one; local readings only ever add confidence to the message, never gate the alert, since the whole point is to warn before local conditions would show it. Runs once per distinct real-world location a tenant's greenhouse Units/Farms or Open-Field parcels/zones resolve to (TenantWeatherLocations, WeatherLocationResolver), since two sites under the same organization can genuinely differ (one frosting over, one not).
    public sealed class FrostAlertEvaluator(
        IServerConfigRepository serverConfigRepo, ITenantRepository tenantRepo, IDeviceFarmUnitRepository unitRepo, IFarmParcelRepository farmParcelRepo,
        IDeviceRepository deviceRepo, IUserRepository userRepo, IWeatherForecastClient weatherClient, INotificationDispatcher dispatcher,
        IOptions<AgrumySettings> settingsOptions, ILogger<FrostAlertEvaluator> logger)
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        // Magnus-formula dew-point spread below which condensation/frost is imminent at that sensor's own location.
        private const double LocalConfirmationSpreadC = 3.0;

        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            string? apiKey = config.WeatherApiKey ?? settings.WeatherApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return; // not configured (neither Server Settings nor appsettings.json's Weather:ApiKey) - inert, same as WeatherEvaluator
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
            IReadOnlyList<(double Lat, double Lon)> locations = await TenantWeatherLocations.ResolveDistinctAsync(unitRepo, farmParcelRepo, tenantId, tenant, config);
            foreach ((double latitude, double longitude) in locations)
            {
                ct.ThrowIfCancellationRequested();
                await RunForLocationAsync(tenantId, latitude, longitude, config, apiKey, ct);
            }
        }

        private async Task RunForLocationAsync(int tenantId, double latitude, double longitude, ServerConfig config, string apiKey, CancellationToken ct)
        {
            WeatherLocationState state = await tenantRepo.WeatherLocationStateGetAsync(tenantId, latitude, longitude);
            int pollMinutes = Math.Max(1, config.WeatherPollIntervalMinutes ?? settings.WeatherPollIntervalMinutes);
            if (state.FrostCheckedAtUtc is DateTimeOffset lastChecked && DateTimeOffset.UtcNow - lastChecked < TimeSpan.FromMinutes(pollMinutes))
            {
                return; // not due yet
            }

            int lookaheadHours = Math.Max(1, config.FrostLookaheadHours ?? settings.FrostLookaheadHours);
            FrostForecastResult? forecast = await weatherClient.GetFrostForecastAsync(latitude, longitude, apiKey, lookaheadHours, ct);
            if (forecast is null)
            {
                return; // fetch failed (already logged in the client) - leave the last good reading in place
            }

            double tempThreshold = config.FrostTempThresholdC ?? settings.FrostTempThresholdC;
            double cloudinessMax = config.FrostCloudinessMaxPercent ?? settings.FrostCloudinessMaxPercent;
            double windMax = config.FrostWindMaxMetersPerSecond ?? settings.FrostWindMaxMetersPerSecond;

            bool frostPredicted = forecast.MinTemperatureC <= tempThreshold
                && (forecast.CloudinessPercent is not double clouds || clouds <= cloudinessMax)
                && (forecast.WindSpeedMetersPerSecond is not double wind || wind <= windMax);

            bool wasPredicted = state.FrostPredicted;
            await tenantRepo.WeatherLocationStateSetFrostAsync(tenantId, latitude, longitude, frostPredicted, frostPredicted ? forecast.HoursAhead : null, DateTimeOffset.UtcNow);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Frost check (tenant {TenantId}, {Lat},{Lon}): {MinTemp}C in {HoursAhead}h, cloudiness {Clouds}%, wind {Wind}m/s -> FrostPredicted={FrostPredicted}.",
                    tenantId, latitude, longitude, forecast.MinTemperatureC, forecast.HoursAhead, forecast.CloudinessPercent, forecast.WindSpeedMetersPerSecond, frostPredicted);
            }

            if (!frostPredicted || wasPredicted)
            {
                return; // no risk, or already alerted for this ongoing forecast episode - dedup until it clears
            }

            string? localConfirmation = await BuildLocalConfirmationAsync(tenantId, ct);
            string message = $"Frost is forecast in about {forecast.HoursAhead} hours " +
                $"(min {forecast.MinTemperatureC:0.#}C, {forecast.CloudinessPercent:0}% cloud cover, {forecast.WindSpeedMetersPerSecond:0.#} m/s wind) - " +
                "conditions favor radiative frost. Consider starting heating or covering sensitive crops." +
                (localConfirmation is null ? "" : $" {localConfirmation}");

            var recipients = await NotificationRecipientBuilder.BuildForTenantAdminsAsync(userRepo, tenantId, NotificationEventType.Frost);
            if (recipients.Count > 0)
            {
                await dispatcher.DispatchToRecipientsAsync("Agrumy: frost predicted", message, NotificationSeverity.Warning, recipients, ct: ct);
            }
        }

        // Best-effort - a device with no recent Temperature+Humidity reading just doesn't contribute a reading; never blocks the forecast-based alert above.
        private async Task<string?> BuildLocalConfirmationAsync(int tenantId, CancellationToken ct)
        {
            IList<FrostSensorReading> readings = await deviceRepo.FrostSensorReadingsGetAsync(tenantId);
            double? minSpread = null;
            foreach (var r in readings)
            {
                ct.ThrowIfCancellationRequested();
                double? dewPoint = DewPointCalculator.Compute(r.Temperature, r.Humidity);
                if (dewPoint is not double dp || r.Temperature is not double t)
                {
                    continue;
                }
                double spread = t - dp;
                if (minSpread is not double best || spread < best)
                {
                    minSpread = spread;
                }
            }
            return minSpread is double s && s <= LocalConfirmationSpreadC
                ? $"Local sensors confirm a narrowing dew-point spread ({s:0.#}C), consistent with imminent frost/condensation."
                : null;
        }
    }
}
