using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Notifications;
using Agrumy.Shared.Utils;

namespace Agrumy.Api.BackgroundWorkers
{
    /// A calibrated zone's fill percent (Agrumy.Shared.Utils.TankCalculator) crossing ServerConfig.TankRefillThreshold (or its organization's own TenantAlertConfig override) fires one alert per low-tank streak, dead-zone-latched against TankRefillHysteresis - same shape as LowBatteryAlertEvaluator, scoped to zones instead of devices.
    public sealed class TankRefillAlertEvaluator(
        IDeviceFarmUnitRepository deviceFarmUnitRepo, IUserRepository userRepo, ITenantRepository tenantRepo, IServerConfigRepository serverConfigRepo, INotificationDispatcher dispatcher)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            var candidates = await deviceFarmUnitRepo.TankRefillAlertCandidatesGetAsync();

            // One TenantAlertConfig lookup per distinct organization in this batch, not per zone.
            var tenantConfigs = new Dictionary<int, TenantAlertConfig>();
            foreach (int tenantId in candidates.Select(z => z.TenantID).Distinct())
            {
                tenantConfigs[tenantId] = await tenantRepo.TenantAlertConfigGetAsync(tenantId);
            }

            foreach (var z in candidates)
            {
                ct.ThrowIfCancellationRequested();

                (double? fillPercent, _) = TankCalculator.Compute(z.WaterLevel, z.WaterLevelRawEmpty, z.WaterLevelRawFull, z.TankCapacityLiters);
                // No reading yet for this zone's calibration is NOT "low" for alerting purposes - nothing to compare against threshold.
                if (fillPercent is not double fill)
                {
                    continue;
                }

                TenantAlertConfig tenantConfig = tenantConfigs[z.TenantID];
                double threshold = tenantConfig.TankRefillThreshold ?? serverConfig.TankRefillThreshold ?? 20.0;
                double hysteresis = Math.Max(0.0, tenantConfig.TankRefillHysteresis ?? serverConfig.TankRefillHysteresis ?? 5.0);
                double clearAt = threshold + hysteresis;

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

                var recipients = await NotificationRecipientBuilder.BuildForTenantAdminsAsync(userRepo, z.TenantID, NotificationEventType.TankRefill);
                if (recipients.Count > 0)
                {
                    await dispatcher.DispatchToRecipientsAsync(
                        $"Agrumy: {zoneLabel} tank needs a refill ({fill:0.#}%)",
                        $"{zoneLabel}'s tank is at {fill:0.#}%, at or below the configured threshold of {threshold:0.#}%.",
                        NotificationSeverity.Warning,
                        recipients,
                        ct: ct);
                }

                await deviceFarmUnitRepo.TankRefillNotifiedSetAsync(z.IDDeviceFarmUnitZone, DateTimeOffset.UtcNow);
            }
        }
    }
}
