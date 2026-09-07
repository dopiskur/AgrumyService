using api.Dal.Interface;
using api.Models;
using api.Notifications;
using api.Utils;

namespace api.BackgroundWorkers
{
    /// A calibrated zone's fill percent (api.Utils.TankCalculator) crossing ServerConfig.TankRefillThreshold fires one alert per low-tank streak, dead-zone-latched against TankRefillHysteresis - same shape as LowBatteryAlertEvaluator, scoped to zones instead of devices.
    public sealed class TankRefillAlertEvaluator(
        IDeviceFarmUnitRepository deviceFarmUnitRepo, IUserRepository userRepo, IServerConfigRepository serverConfigRepo, INotificationDispatcher dispatcher)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);

            double threshold = serverConfig.TankRefillThreshold ?? 20.0;
            double hysteresis = Math.Max(0.0, serverConfig.TankRefillHysteresis ?? 5.0);
            double clearAt = threshold + hysteresis;

            var candidates = await deviceFarmUnitRepo.TankRefillAlertCandidatesGetAsync();

            foreach (var z in candidates)
            {
                ct.ThrowIfCancellationRequested();

                (double? fillPercent, _) = TankCalculator.Compute(z.WaterLevel, z.WaterLevelRawEmpty, z.WaterLevelRawFull, z.TankCapacityLiters);
                // No reading yet for this zone's calibration is NOT "low" for alerting purposes - nothing to compare against threshold.
                if (fillPercent is not double fill)
                {
                    continue;
                }

                bool low = fill <= threshold;
                bool recovered = fill >= clearAt;

                if (!low)
                {
                    // Only clear once the reading is fully back OUT of the dead zone (>= clearAt), not merely "not low" (> threshold), or it could spuriously re-fire next tick.
                    if (recovered && z.TankRefillNotifiedAt is not null)
                    {
                        await deviceFarmUnitRepo.TankRefillNotifiedSetAsync(z.IDDeviceFarmUnitZone, null);
                    }
                    continue;
                }

                if (z.TankRefillNotifiedAt is not null)
                {
                    continue; // already alerted for this ongoing low-tank streak - dedup
                }

                string zoneLabel = string.IsNullOrWhiteSpace(z.DeviceFarmUnitZoneName) ? $"Zone {z.IDDeviceFarmUnitZone}" : z.DeviceFarmUnitZoneName;

                var admins = await userRepo.TenantAdminsGetAsync(z.TenantID);
                var recipients = admins.Where(a => !string.IsNullOrWhiteSpace(a.Email)).Select(a => new NotificationRecipient(Email: a.Email)).ToList();
                if (recipients.Count > 0)
                {
                    await dispatcher.DispatchToRecipientsAsync(
                        $"Agrumy: {zoneLabel} tank needs a refill ({fill:0.#}%)",
                        $"{zoneLabel}'s tank is at {fill:0.#}%, at or below the configured threshold of {threshold:0.#}%.",
                        NotificationSeverity.Warning,
                        recipients,
                        ct: ct);
                }

                await deviceFarmUnitRepo.TankRefillNotifiedSetAsync(z.IDDeviceFarmUnitZone, DateTime.UtcNow);
            }
        }
    }
}
