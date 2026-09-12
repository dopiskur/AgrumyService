using Agrumy.Dal;
using Agrumy.Shared;
using System.Text.Json;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Dal
{
    internal sealed partial class EfDeviceFarmUnitRepository
    {
        // ---- Tank refill alert (roadmap #234) --------------------------

        public async Task<IList<TankRefillAlertCandidate>> TankRefillAlertCandidatesGetAsync()
        {
            var zones = await db.DeviceFarmUnitZones.AsNoTracking()
                .Where(z => z.TenantID != null
                    && z.TankCapacityLiters != null && z.WaterLevelRawEmpty != null && z.WaterLevelRawFull != null)
                .ToListAsync();

            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            var result = new List<TankRefillAlertCandidate>();
            foreach (var z in zones)
            {
                // Latest reading per device in the zone (portable scalar subquery, same shape as LowBatteryAlertCandidatesGetAsync's Battery column), averaged client-side.
                var latestPerDevice = await db.Devices.AsNoTracking()
                    .Where(d => d.DeviceFarmUnitZoneID == z.IDDeviceFarmUnitZone)
                    .Select(d => new
                    {
                        d.SleepSeconds,
                        Reading = db.SensorData.AsNoTracking()
                            .Where(s => s.DeviceID == d.IDDevice)
                            .OrderByDescending(s => s.DateCreated)
                            .Select(s => new { s.WaterLevel, s.DateCreated })
                            .FirstOrDefault(),
                    })
                    .ToListAsync();

                // Same staleness window DeviceFarmUnitZoneDashboardGetAsync uses (roadmap #345) - a dead/unreachable sensor's stale last reading must not count toward the average, or the alert never fires despite an actually-empty tank.
                double? waterLevel = latestPerDevice
                    .Where(d => d.Reading?.DateCreated != null && (utcNow - d.Reading.DateCreated.Value).TotalSeconds <=
                        (d.SleepSeconds ?? 60) * (double)DeviceFleetStatus.OfflineMissedPolls + DeviceFleetStatus.OfflineGraceSeconds)
                    .Select(d => (double?)d.Reading!.WaterLevel)
                    .Average();

                result.Add(new TankRefillAlertCandidate(
                    z.IDDeviceFarmUnitZone, z.TenantID!.Value, z.DeviceFarmUnitZoneName,
                    waterLevel, z.WaterLevelRawEmpty, z.WaterLevelRawFull, z.TankCapacityLiters, z.TankRefillNotifiedAt));
            }
            return result;
        }

        public async Task TankRefillNotifiedSetAsync(int idDeviceFarmUnitZone, DateTimeOffset? notifiedAt)
        {
            await db.DeviceFarmUnitZones.Where(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone)
                .ExecuteUpdateAsync(s => s.SetProperty(z => z.TankRefillNotifiedAt, notifiedAt));
        }

        // ---- Manual actuate (roadmap #219) --------------------------

        public async Task ManualOverrideStartAsync(DeviceManualOverride manualOverride)
        {
            var row = await db.DeviceManualOverrides
                .FirstOrDefaultAsync(o => o.DeviceID == manualOverride.DeviceID && o.RelayFunction == (int)manualOverride.RelayFunction);
            if (row == null)
            {
                row = new DeviceManualOverrideRow { DeviceID = manualOverride.DeviceID, RelayFunction = (int)manualOverride.RelayFunction };
                db.DeviceManualOverrides.Add(row);
            }
            row.TenantID = manualOverride.TenantID;
            row.Mode = (int)manualOverride.Mode;
            row.StartedAtUtc = manualOverride.StartedAtUtc;
            row.ExpiresAtUtc = manualOverride.ExpiresAtUtc;
            row.TargetMetric = (int?)manualOverride.TargetMetric;
            row.TargetThreshold = manualOverride.TargetThreshold;
            row.TargetHysteresis = manualOverride.TargetHysteresis;
            await db.SaveChangesAsync();
        }

        public async Task ManualOverrideStopAsync(int deviceId, RelayFunction relayFunction)
        {
            await db.DeviceManualOverrides
                .Where(o => o.DeviceID == deviceId && o.RelayFunction == (int)relayFunction)
                .ExecuteDeleteAsync();
        }

        public async Task<IList<DeviceManualOverride>> ManualOverridesActiveForDeviceAsync(int deviceId)
        {
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;
            var rows = await db.DeviceManualOverrides.AsNoTracking()
                .Where(o => o.DeviceID == deviceId && o.ExpiresAtUtc > utcNow)
                .ToListAsync();
            return rows.Select(ToDtoManualOverride).ToList();
        }

        private static DeviceManualOverride ToDtoManualOverride(DeviceManualOverrideRow o) => new()
        {
            IDDeviceManualOverride = o.IDDeviceManualOverride,
            DeviceID = o.DeviceID,
            TenantID = o.TenantID,
            RelayFunction = (RelayFunction)o.RelayFunction,
            Mode = (ManualOverrideMode)o.Mode,
            StartedAtUtc = o.StartedAtUtc,
            ExpiresAtUtc = o.ExpiresAtUtc,
            TargetMetric = o.TargetMetric is int tm ? (SensorMetric)tm : null,
            TargetThreshold = o.TargetThreshold,
            TargetHysteresis = o.TargetHysteresis,
        };
    }
}
