using Agrumy.Shared;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;
using Agrumy.Api.Weather;
using Agrumy.Api.Notifications;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Forecast-based early warning (temperature + cloudiness + wind - the three factors radiative frost actually depends on) fires hours before frost sets in, refined with a same-tick local DewPoint-spread reading where sensors report one; local readings only ever add confidence to the message, never gate the alert, since the whole point is to warn before local conditions would show it.
    public sealed class FrostAlertEvaluator(
        IServerConfigRepository serverConfigRepo, IDeviceRepository deviceRepo, IUserRepository userRepo,
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
            if (config.WeatherLocationLat is not double lat || config.WeatherLocationLon is not double lon)
            {
                return; // null = admin hasn't set a location yet
            }

            int pollMinutes = Math.Max(1, config.WeatherPollIntervalMinutes ?? settings.WeatherPollIntervalMinutes);
            if (config.FrostCheckedAtUtc is DateTimeOffset lastChecked && DateTimeOffset.UtcNow - lastChecked < TimeSpan.FromMinutes(pollMinutes))
            {
                return; // not due yet
            }

            int lookaheadHours = Math.Max(1, config.FrostLookaheadHours ?? settings.FrostLookaheadHours);
            FrostForecastResult? forecast = await weatherClient.GetFrostForecastAsync(lat, lon, settings.WeatherApiKey, lookaheadHours, ct);
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

            bool wasPredicted = config.FrostPredicted;
            await serverConfigRepo.ServerConfigFrostStateSetAsync(frostPredicted, frostPredicted ? forecast.HoursAhead : null, DateTimeOffset.UtcNow, 1);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Frost check: {MinTemp}C in {HoursAhead}h, cloudiness {Clouds}%, wind {Wind}m/s -> FrostPredicted={FrostPredicted}.",
                    forecast.MinTemperatureC, forecast.HoursAhead, forecast.CloudinessPercent, forecast.WindSpeedMetersPerSecond, frostPredicted);
            }

            if (!frostPredicted || wasPredicted)
            {
                return; // no risk, or already alerted for this ongoing forecast episode - dedup until it clears
            }

            string? localConfirmation = await BuildLocalConfirmationAsync(ct);
            string message = $"Frost is forecast in about {forecast.HoursAhead} hours " +
                $"(min {forecast.MinTemperatureC:0.#}C, {forecast.CloudinessPercent:0}% cloud cover, {forecast.WindSpeedMetersPerSecond:0.#} m/s wind) - " +
                "conditions favor radiative frost. Consider starting heating or covering sensitive crops." +
                (localConfirmation is null ? "" : $" {localConfirmation}");

            // tenantId 0 = GlobalAdmin, same convention TenantAdminsGetAsync already uses - the forecast location is install-wide, not per-tenant.
            var recipients = await NotificationRecipientBuilder.BuildForTenantAdminsAsync(userRepo, 0, NotificationEventType.Frost);
            if (recipients.Count > 0)
            {
                await dispatcher.DispatchToRecipientsAsync("Agrumy: frost predicted", message, NotificationSeverity.Warning, recipients, ct: ct);
            }
        }

        // Best-effort - a device with no recent Temperature+Humidity reading just doesn't contribute a reading; never blocks the forecast-based alert above.
        private async Task<string?> BuildLocalConfirmationAsync(CancellationToken ct)
        {
            IList<FrostSensorReading> readings = await deviceRepo.FrostSensorReadingsGetAsync();
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
