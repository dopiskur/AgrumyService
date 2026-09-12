using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Notifications;
using Agrumy.Api.Diagnostics;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Offline-detection/notification logic, kept separate from OfflineAlertBackgroundService so it is directly unit-testable with mocked repositories.
    public sealed class OfflineAlertEvaluator(IDeviceRepository deviceRepo, IUserRepository userRepo, INotificationDispatcher dispatcher, AgrumyMetrics metrics)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            var candidates = await deviceRepo.OfflineAlertCandidatesGetAsync();

            // Enabled-device fleet snapshot this same query already gives for free - a never-seen device counts as offline here, unlike the alerting loop below which deliberately skips it.
            if (candidates.Count > 0)
            {
                int offlineCount = candidates.Count(d => !DeviceFleetStatus.ComputeOnline(d.LastSeenAt, d.SleepSeconds, utcNow));
                metrics.SetOfflineDevicePercentage(100.0 * offlineCount / candidates.Count);
            }

            foreach (var d in candidates)
            {
                ct.ThrowIfCancellationRequested();

                // Never-seen devices are NOT "offline" for alerting purposes, unlike the Fleet dashboard badge - alerting is opt-in to "was reachable, now is not."
                if (d.LastSeenAt is null)
                {
                    continue;
                }

                bool online = DeviceFleetStatus.ComputeOnline(d.LastSeenAt, d.SleepSeconds, utcNow);

                if (online)
                {
                    if (d.OfflineNotifiedAt is not null)
                    {
                        // Clears the mark so the NEXT offline streak alerts fresh instead of staying silent forever after the first incident.
                        await deviceRepo.DeviceOfflineNotifiedSetAsync(d.IDDevice, null);
                    }
                    continue;
                }

                if (d.OfflineNotifiedAt is not null)
                {
                    continue; // already alerted for this ongoing streak - dedup
                }
                // A genuinely organization-less device has no organization admins to notify.
                if (d.TenantID is not int tenantId)
                {
                    continue;
                }

                string deviceLabel = string.IsNullOrWhiteSpace(d.DeviceName) ? $"Device {d.IDDevice}" : d.DeviceName;

                // Same eventDevice table/timeline as device-pushed events - server-detected, not device-pushed, but should appear alongside them on the Events page.
                await deviceRepo.EventDevicePushAsync(d.IDDevice, tenantId, DeviceEventType.Offline,
                    $"No contact since {d.LastSeenAt:u}");

                var recipients = await NotificationRecipientBuilder.BuildForTenantAdminsAsync(userRepo, tenantId, NotificationEventType.Offline);
                if (recipients.Count > 0)
                {
                    await dispatcher.DispatchToRecipientsAsync(
                        $"Agrumy: {deviceLabel} is offline",
                        $"{deviceLabel} has not reported in since {d.LastSeenAt:u} UTC.",
                        NotificationSeverity.Warning,
                        recipients,
                        ct: ct);
                }

                await deviceRepo.DeviceOfflineNotifiedSetAsync(d.IDDevice, utcNow);
            }
        }
    }
}
