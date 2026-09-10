using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Notifications;

namespace Agrumy.Api.BackgroundWorkers
{
    /// A device's latest battery reading crossing ServerConfig.BatteryLowThreshold (or its tenant's own TenantAlertConfig override, roadmap #509) fires one alert per low-battery streak, dead-zone-latched against BatteryLowHysteresis to avoid chattering at the boundary.
    public sealed class LowBatteryAlertEvaluator(
        IDeviceRepository deviceRepo, IUserRepository userRepo, ITenantRepository tenantRepo, IServerConfigRepository serverConfigRepo, INotificationDispatcher dispatcher)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            var candidates = await deviceRepo.LowBatteryAlertCandidatesGetAsync();

            // One TenantAlertConfig lookup per distinct tenant in this batch, not per device.
            var tenantConfigs = new Dictionary<int, TenantAlertConfig>();
            foreach (int tenantId in candidates.Where(d => d.TenantID != null).Select(d => d.TenantID!.Value).Distinct())
            {
                tenantConfigs[tenantId] = await tenantRepo.TenantAlertConfigGetAsync(tenantId);
            }

            foreach (var d in candidates)
            {
                ct.ThrowIfCancellationRequested();

                // Never-reported battery is NOT "low" for alerting purposes - nothing to compare against threshold.
                if (d.Battery is not int battery)
                {
                    continue;
                }
                // Roadmap #406 - a genuinely tenant-less device has no tenant admins to notify.
                if (d.TenantID is not int tenantId)
                {
                    continue;
                }

                TenantAlertConfig tenantConfig = tenantConfigs[tenantId];
                double threshold = tenantConfig.BatteryLowThreshold ?? serverConfig.BatteryLowThreshold ?? 20.0;
                double hysteresis = Math.Max(0.0, tenantConfig.BatteryLowHysteresis ?? serverConfig.BatteryLowHysteresis ?? 5.0);
                double clearAt = threshold + hysteresis;

                bool low = battery <= threshold;
                bool recovered = battery >= clearAt;

                if (!low)
                {
                    // Only clear once the reading is fully back OUT of the dead zone (>= clearAt), not merely "not low" (> threshold), or it could spuriously re-fire next tick.
                    if (recovered && d.LowBatteryNotifiedAt is not null)
                    {
                        await deviceRepo.DeviceLowBatteryNotifiedSetAsync(d.IDDevice, null);
                    }
                    continue;
                }

                if (d.LowBatteryNotifiedAt is not null)
                {
                    continue; // already alerted for this ongoing low-battery streak - dedup
                }

                string deviceLabel = string.IsNullOrWhiteSpace(d.DeviceName) ? $"Device {d.IDDevice}" : d.DeviceName;

                // Same eventDevice table/timeline as device-pushed events - server-detected, not device-pushed, same as Offline.
                await deviceRepo.EventDevicePushAsync(d.IDDevice, tenantId, DeviceEventType.LowBattery,
                    $"Battery at {battery}% (threshold {threshold:0.#}%)");

                var recipients = await NotificationRecipientBuilder.BuildForTenantAdminsAsync(userRepo, tenantId, NotificationEventType.LowBattery);
                if (recipients.Count > 0)
                {
                    await dispatcher.DispatchToRecipientsAsync(
                        $"Agrumy: {deviceLabel} battery is low ({battery}%)",
                        $"{deviceLabel} last reported {battery}% battery, at or below the configured threshold of {threshold:0.#}%.",
                        NotificationSeverity.Warning,
                        recipients,
                        ct: ct);
                }

                await deviceRepo.DeviceLowBatteryNotifiedSetAsync(d.IDDevice, DateTimeOffset.UtcNow);
            }
        }
    }
}
