using Agrumy.Shared;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;
using Agrumy.Api.Weather;
using Agrumy.Api.Notifications;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Forecast-based early warning (temperature + cloudiness + wind - the three factors radiative frost actually depends on) fires hours before frost sets in, refined with a same-tick local DewPoint-spread reading where sensors report one; local readings only ever add confidence to the message, never gate the alert, since the whole point is to warn before local conditions would show it. Runs once per organization, since each organization can sit at a genuinely different physical site (Tenant.Latitude/Longitude, falling back to ServerConfig.WeatherLocationLat/Lon - same cascade AstronomicalRuleResolver's callers already use).
    public sealed class FrostAlertEvaluator(
        IServerConfigRepository serverConfigRepo, ITenantRepository tenantRepo, IDeviceRepository deviceRepo, IUserRepository userRepo,
        IWeatherForecastClient weatherClient, INotificationDispatcher dispatcher, IOptions<AgrumySettings> settingsOptions,
        ILogger<FrostAlertEvaluator> logger)
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        // Magnus-formula dew-point spread below which condensation/frost is imminent at that sensor's own location.
        private const double LocalConfirmationSpreadC = 3.0;

        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(settings.WeatherApiKey))
            {
                return; // not configured (Weather:ApiKey unset) - inert, same as WeatherEvaluator
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
                return; // null = neither this organization nor the server-wide default has a location set yet
            }

            TenantWeatherState state = await tenantRepo.TenantWeatherStateGetAsync(tenantId);
            int pollMinutes = Math.Max(1, config.WeatherPollIntervalMinutes ?? settings.WeatherPollIntervalMinutes);
            if (state.FrostCheckedAtUtc is DateTimeOffset lastChecked && DateTimeOffset.UtcNow - lastChecked < TimeSpan.FromMinutes(pollMinutes))
            {
                return; // not due yet
            }

            int lookaheadHours = Math.Max(1, config.FrostLookaheadHours ?? settings.FrostLookaheadHours);
            FrostForecastResult? forecast = await weatherClient.GetFrostForecastAsync(latitude, longitude, settings.WeatherApiKey!, lookaheadHours, ct);
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
            await tenantRepo.TenantWeatherStateSetFrostAsync(tenantId, frostPredicted, frostPredicted ? forecast.HoursAhead : null, DateTimeOffset.UtcNow);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Frost check (tenant {TenantId}): {MinTemp}C in {HoursAhead}h, cloudiness {Clouds}%, wind {Wind}m/s -> FrostPredicted={FrostPredicted}.",
                    tenantId, forecast.MinTemperatureC, forecast.HoursAhead, forecast.CloudinessPercent, forecast.WindSpeedMetersPerSecond, frostPredicted);
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
