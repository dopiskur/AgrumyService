using Agrumy.Dal;
using Agrumy.Shared;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Firmware;
using Agrumy.Api.Quota;
using Agrumy.Api.Security;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Agrumy.Api.Dal
{
    internal sealed partial class EfDeviceRepository
    {
        // ---- Device events -----------------------------------------------

        public async Task<bool> EventDevicePushAsync(int deviceID, int tenantID, DeviceEventType eventType, string? message)
        {
            // ServerConfigGetAsync may auto-generate the row (and its EventDedupeMinutes default) on a brand-new install, same as DeviceAddAsync's own call. Organization's own override wins over the server-wide default.
            int? tenantDedupeMinutes = await db.Tenants.AsNoTracking().Where(t => t.IDTenant == tenantID).Select(t => t.EventDedupeMinutes).FirstOrDefaultAsync();
            int dedupeMinutes = tenantDedupeMinutes ?? (await serverConfigRepository.ServerConfigGetAsync(1)).EventDedupeMinutes ?? settings.EventDedupeMinutes;
            DateTime cutoff = DateTime.UtcNow.AddMinutes(-dedupeMinutes);

            bool isDuplicate = await db.EventDevices.AsNoTracking()
                .AnyAsync(e => e.DeviceID == deviceID && e.EventID == (int)eventType && e.Date >= cutoff);
            if (isDuplicate)
            {
                return false;
            }

            db.EventDevices.Add(new EventDeviceRow
            {
                DeviceID = deviceID,
                TenantID = tenantID,
                EventID = (int)eventType,
                Date = DateTime.UtcNow, // server clock, not device-reported - a device mid-"NoInternet" may lack NTP sync
                Message = message,
            });
            await db.SaveChangesAsync();
            return true;
        }

        public async Task<IList<DeviceEvent>> EventDeviceGetAsync(int? deviceID, int? tenantID, int limit = 100)
        {
            var rows = await db.EventDevices.AsNoTracking()
                .Where(e => e.DeviceID == deviceID && e.TenantID == tenantID)
                .OrderByDescending(e => e.Date)
                .Take(limit)
                .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<bool> EventDeviceAcknowledgeAsync(int idEventDevice, int? tenantID)
        {
            // tenantID is the same value used to authorize the call, applied straight to the WHERE clause - a foreign organization's event id can never be acknowledged even if guessable.
            IQueryable<EventDeviceRow> q = db.EventDevices.Where(e => e.IDEventDevice == idEventDevice);
            if (tenantID != null)
            {
                q = q.Where(e => e.TenantID == tenantID);
            }
            int updated = await q.ExecuteUpdateAsync(s => s.SetProperty(e => e.AcknowledgedAt, DateTime.UtcNow));
            return updated > 0;
        }

        // ---- Offline alert background worker ------------------------------

        public async Task<IList<OfflineAlertCandidate>> OfflineAlertCandidatesGetAsync()
        {
            return await db.Devices.AsNoTracking()
                .Where(d => d.Enabled == true) // a disabled device is expected to be silent
                .Select(d => new OfflineAlertCandidate(
                    d.IDDevice,
                    d.TenantID,
                    d.DeviceName,
                    d.SleepSeconds,
                    db.DeviceDiagnostics.AsNoTracking().Where(x => x.DeviceID == d.IDDevice).Select(x => x.LastSeenAt).FirstOrDefault(),
                    db.DeviceDiagnostics.AsNoTracking().Where(x => x.DeviceID == d.IDDevice).Select(x => x.OfflineNotifiedAt).FirstOrDefault()))
                .ToListAsync();
        }

        public async Task DeviceOfflineNotifiedSetAsync(int deviceID, DateTimeOffset? notifiedAt)
        {
            // A device with no diagnostic row has never polled, so it can't have just transitioned to offline - nothing to set.
            await db.DeviceDiagnostics
                .Where(x => x.DeviceID == deviceID)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.OfflineNotifiedAt, notifiedAt));
        }

        // ---- Low-battery alert background worker --------------------------

        public async Task<IList<LowBatteryAlertCandidate>> LowBatteryAlertCandidatesGetAsync()
        {
            // Same correlated-scalar-subquery shape as DeviceFleetGetAsync's Battery column above.
            return await db.Devices.AsNoTracking()
                .Where(d => d.Enabled == true) // a disabled device is expected to be silent
                .Select(d => new LowBatteryAlertCandidate(
                    d.IDDevice,
                    d.TenantID,
                    d.DeviceName,
                    db.SensorData.AsNoTracking()
                        .Where(s => s.DeviceID == d.IDDevice)
                        .OrderByDescending(s => s.DateCreated)
                        .Select(s => s.Battery)
                        .FirstOrDefault(),
                    db.DeviceDiagnostics.AsNoTracking().Where(x => x.DeviceID == d.IDDevice).Select(x => x.LowBatteryNotifiedAt).FirstOrDefault()))
                .ToListAsync();
        }

        public async Task DeviceLowBatteryNotifiedSetAsync(int deviceID, DateTimeOffset? notifiedAt)
        {
            // Same "nothing to set for a device that has never polled" rule as DeviceOfflineNotifiedSetAsync.
            await db.DeviceDiagnostics
                .Where(x => x.DeviceID == deviceID)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LowBatteryNotifiedAt, notifiedAt));
        }

        // ---- Frost alert background worker ---------------------------------

        public async Task<IList<FrostSensorReading>> FrostSensorReadingsGetAsync(int tenantId)
        {
            return await db.Devices.AsNoTracking()
                .Where(d => d.Enabled == true && d.TenantID == tenantId)
                .Select(d => new FrostSensorReading(
                    db.SensorData.AsNoTracking().Where(s => s.DeviceID == d.IDDevice).OrderByDescending(s => s.DateCreated).Select(s => s.Temperature).FirstOrDefault(),
                    db.SensorData.AsNoTracking().Where(s => s.DeviceID == d.IDDevice).OrderByDescending(s => s.DateCreated).Select(s => s.Humidity).FirstOrDefault()))
                .ToListAsync();
        }

        private static DeviceEvent ToDto(EventDeviceRow e) => new()
        {
            IDEventDevice = e.IDEventDevice,
            DeviceID = e.DeviceID,
            // Guards against a row written by a future/older enum definition - never throws, just surfaces the raw number.
            EventType = Enum.IsDefined(typeof(DeviceEventType), e.EventID)
                ? ((DeviceEventType)e.EventID).ToString()
                : $"Unknown({e.EventID})",
            Message = e.Message,
            CreatedAt = e.Date,
        };
    }
}
