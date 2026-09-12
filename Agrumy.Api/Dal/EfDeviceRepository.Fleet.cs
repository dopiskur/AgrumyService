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
        // ---- Legacy board-less OTA lookup (board-keyed catalog is IFirmwareRepository) --

        public async Task<DeviceFirmware?> DeviceFirmwareLatestGetAsync(int? deviceTypeID)
        {
            var row = await db.DeviceFirmwares.AsNoTracking()
                .Where(f => f.DeviceTypeID == deviceTypeID)
                .OrderByDescending(f => f.DateAdded)
                .FirstOrDefaultAsync();
            return row == null ? null : EfFirmwareRepository.FirmwareToDto(row);
        }

        // ---- Device diagnostics / fleet ---------------------------------

        public async Task DeviceDiagnosticUpsertAsync(int deviceID, int tenantID, DeviceConfigPoll poll)
        {
            // "" (every generic esp32dev/esp32s3usbotg build) is normalized to null here rather than resolved to a DeviceTypeID - a device with no specific kit stores no FK at all.
            string? kit = string.IsNullOrEmpty(poll.Kit) ? null : poll.Kit;
            int? deviceTypeId = kit != null ? await EnsureDeviceTypeRegisteredAsync(kit) : null;

            var row = await db.DeviceDiagnostics.FirstOrDefaultAsync(d => d.DeviceID == deviceID);
            if (row == null)
            {
                row = new DeviceDiagnosticRow { DeviceID = deviceID };
                db.DeviceDiagnostics.Add(row);
            }

            row.TenantID = tenantID;
            row.LastSeenAt = DateTime.UtcNow; // server clock - device clocks drift and may lack NTP, same rule as EventDevicePushAsync
            // Keep the last known value when a field is missing so upgrading the server alone doesn't blank existing diagnostics.
            row.UptimeSeconds = poll.Uptime ?? row.UptimeSeconds;
            row.RssiDbm = poll.Rssi ?? row.RssiDbm;
            row.FreeHeapBytes = poll.FreeHeap ?? row.FreeHeapBytes;
            row.MinFreeHeapBytes = poll.MinFreeHeap ?? row.MinFreeHeapBytes;
            row.MaxAllocHeapBytes = poll.MaxAllocHeap ?? row.MaxAllocHeapBytes;
            row.StackHighWaterMarkBytes = poll.StackHighWaterMark ?? row.StackHighWaterMarkBytes;
            row.NetworkStackHighWaterMarkBytes = poll.NetworkStackHighWaterMark ?? row.NetworkStackHighWaterMarkBytes;
            row.ConfigSchemaVersion = poll.ConfigSchemaVersion ?? row.ConfigSchemaVersion;
            // Self-reported, unbounded in principle (`git describe --dirty` dev builds run long) - truncated to the column's own cap rather than trusting every future firmware build to stay under it, so a heartbeat write can never fail the whole Config poll over diagnostics text.
            row.FirmwareVersion = poll.FirmwareVersion is string fv && fv.Length > 40 ? fv[..40] : poll.FirmwareVersion ?? row.FirmwareVersion;
            row.Board = poll.Board ?? row.Board;
            row.DeviceTypeID = deviceTypeId ?? row.DeviceTypeID;
            await db.SaveChangesAsync();

            // A GPS fix always wins over a manual location - writes to the Device row itself, not DeviceDiagnostics, since it's a location other features can read, not a point-in-time heartbeat metric.
            if (poll.Latitude.HasValue && poll.Longitude.HasValue)
            {
                await db.Devices.Where(d => d.IDDevice == deviceID)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(d => d.Latitude, poll.Latitude)
                        .SetProperty(d => d.Longitude, poll.Longitude)
                        .SetProperty(d => d.LocationSource, (int)DeviceLocationSource.Gps));
            }
        }

        public async Task<bool> DeviceCheckAndRecordSensorPushAsync(int deviceID, TimeSpan minInterval)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTimeOffset cutoff = now - minInterval;
            int rows = await db.DeviceDiagnostics
                .Where(d => d.DeviceID == deviceID && (d.LastSensorPushAt == null || d.LastSensorPushAt <= cutoff))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSensorPushAt, now));
            return rows > 0;
        }

        /// deviceDiagnostic.DeviceTypeID has a real FK to deviceType.IDDeviceType - an unrecognized Kit string must never block the device's own heartbeat write because of it, so it's auto-registered here (ControllerCapable=false) in its own save, BEFORE the diagnostic row; a concurrent duplicate insert from another device reporting the same brand-new kit is tolerated by re-fetching the winner's id, not retried.
        private async Task<int> EnsureDeviceTypeRegisteredAsync(string kit)
        {
            int? existingId = await db.DeviceTypes.AsNoTracking().Where(t => t.Kit == kit).Select(t => (int?)t.IDDeviceType).FirstOrDefaultAsync();
            if (existingId is int id)
            {
                return id;
            }
            var candidate = new DeviceTypeRow { Kit = kit, ControllerCapable = false };
            db.DeviceTypes.Add(candidate);
            try
            {
                await db.SaveChangesAsync();
                return candidate.IDDeviceType;
            }
            catch (DbUpdateException ex) when (DbExceptionClassifier.Classify(ex) == DbFailureKind.ConstraintViolation)
            {
                db.Entry(candidate).State = EntityState.Detached; // lost the race - detach so it isn't re-inserted (and re-fails) by the caller's own SaveChangesAsync right after this returns.
                return await db.DeviceTypes.AsNoTracking().Where(t => t.Kit == kit).Select(t => t.IDDeviceType).FirstAsync();
            }
        }

        public async Task<IList<DeviceFleetStatus>> DeviceFleetGetAsync(int? tenantID)
        {
            string cacheKey = CacheKeys.Fleet(tenantID);
            List<DeviceFleetStatus>? cached = await cache.GetAsync<List<DeviceFleetStatus>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            IQueryable<DeviceRow> devices = db.Devices.AsNoTracking();
            if (tenantID != null)
            {
                devices = devices.Where(d => d.TenantID == tenantID);
            }

            List<DeviceFleetStatus> result = (await BuildFleetStatusesAsync(devices))
                // Fleet page default view: unassigned devices surfaced first, newest device first within each group.
                .OrderBy(d => d.DeviceFarmUnitID == null ? 0 : 1)
                .ThenByDescending(d => d.IDDevice)
                .ToList();

            await cache.SetAsync(cacheKey, result, CacheKeys.FleetTtl);
            return result;
        }

        /// A write that changes a device's fleet row (e.g. zone assignment) must drop both its own-organization and the GlobalAdmin's cached snapshot, or the next Fleet read can still serve the pre-write result for up to CacheKeys.FleetTtl. Public (not private) since EfRepository.DeviceFarmUnits.cs (not yet extracted) also calls it after assign/unassign.
        public Task InvalidateFleetCacheAsync(int? tenantID) => Task.WhenAll(
            cache.RemoveAsync(CacheKeys.Fleet(null)),
            tenantID != null ? cache.RemoveAsync(CacheKeys.Fleet(tenantID)) : Task.CompletedTask);

        /// Same status one row of DeviceFleetGetAsync would carry, without loading the rest of the fleet - for a single-device detail page. Not cached (DeviceFleetGetAsync's cache exists to share one whole-fleet scan across concurrent Fleet page tabs, not relevant to a one-row lookup).
        public async Task<DeviceFleetStatus?> DeviceFleetStatusGetAsync(int deviceID, int? tenantID)
        {
            IQueryable<DeviceRow> devices = db.Devices.AsNoTracking().Where(d => d.IDDevice == deviceID);
            if (tenantID != null)
            {
                devices = devices.Where(d => d.TenantID == tenantID);
            }

            return (await BuildFleetStatusesAsync(devices)).FirstOrDefault();
        }

        // Left-join diagnostics (a never-seen device still shows on the dashboard) - Battery is a correlated scalar subquery (plain ORDER BY...LIMIT 1, no LATERAL needed since MariaDB lacks it).
        private async Task<List<DeviceFleetStatus>> BuildFleetStatusesAsync(IQueryable<DeviceRow> devices)
        {
            var rows = await devices
                .Select(d => new
                {
                    Device = d,
                    Diag = db.DeviceDiagnostics.AsNoTracking()
                        .Where(x => x.DeviceID == d.IDDevice)
                        .FirstOrDefault(),
                    Battery = db.SensorData.AsNoTracking()
                        .Where(s => s.DeviceID == d.IDDevice)
                        .OrderByDescending(s => s.DateCreated)
                        .Select(s => s.Battery)
                        .FirstOrDefault(),
                })
                .ToListAsync();

            // DeviceType catalog is a small fixed set - cheap to pull entire and check in memory rather than a per-device join.
            Dictionary<int, DeviceTypeRow> deviceTypesById = await db.DeviceTypes.AsNoTracking()
                .ToDictionaryAsync(k => k.IDDeviceType);

            // Units/Zones are a small admin-managed set - same in-memory-lookup reasoning as kitCapability above.
            Dictionary<int, string?> unitNames = await db.DeviceFarmUnits.AsNoTracking()
                .ToDictionaryAsync(u => u.IDDeviceFarmUnit, u => u.DeviceFarmUnitName);
            Dictionary<int, string?> zoneNames = await db.DeviceFarmUnitZones.AsNoTracking()
                .ToDictionaryAsync(z => z.IDDeviceFarmUnitZone, z => z.DeviceFarmUnitZoneName);

            // Fleet's "Farm" column resolves the top-level DeviceFarm regardless of branch (Unit->Farm for Greenhouse, Sowing->Farm directly for Open-Field, restructure R); same in-memory-lookup reasoning as unitNames/zoneNames above.
            Dictionary<int, string?> farmNames = await db.DeviceFarms.AsNoTracking()
                .ToDictionaryAsync(f => f.IDDeviceFarm, f => f.DeviceFarmName);
            Dictionary<int, int?> unitFarmIds = await db.DeviceFarmUnits.AsNoTracking()
                .ToDictionaryAsync(u => u.IDDeviceFarmUnit, u => u.DeviceFarmID);
            Dictionary<int, int> sowingFarmIds = await db.Sowings.AsNoTracking()
                .ToDictionaryAsync(s => s.IDSowing, s => s.FarmID);

            string? FarmNameFor(DeviceRow device)
            {
                if (device.DeviceFarmUnitID is int unitId && unitFarmIds.TryGetValue(unitId, out int? unitFarmId) && unitFarmId is int fid)
                {
                    return farmNames.GetValueOrDefault(fid);
                }
                if (device.SowingID is int cropId && sowingFarmIds.TryGetValue(cropId, out int sowingFarmId))
                {
                    return farmNames.GetValueOrDefault(sowingFarmId);
                }
                return null;
            }

            // One bulk read of every relay state for devices in this result set, grouped in memory - same reasoning as kitCapability above (a handful of rows per device, not worth a per-device round trip).
            var deviceIds = rows.Select(r => r.Device.IDDevice).ToList();
            HashSet<int> virtualDeviceIds = (await db.DeviceVirtuals.AsNoTracking()
                .Where(v => deviceIds.Contains(v.DeviceID))
                .Select(v => v.DeviceID)
                .ToListAsync()).ToHashSet();
            Dictionary<int, List<ControllerDataStatus>> relayStates = (await db.ControllerData.AsNoTracking()
                .Where(c => deviceIds.Contains(c.DeviceID))
                .ToListAsync())
                .GroupBy(c => c.DeviceID)
                .ToDictionary(g => g.Key, g => g.Select(c => new ControllerDataStatus { RelayFunction = (RelayFunction)c.RelayFunction, IsOn = c.IsOn, DateChanged = c.DateChanged }).ToList());

            // One catalog read, newest version per board picked in memory by semver (not DateAdded).
            FirmwareSource activeSource = (await serverConfigRepository.ServerConfigGetAsync(1)).FirmwareSource;
            var visible = new HashSet<int> { (int)activeSource, (int)FirmwareSource.Local };
            var catalog = await db.DeviceFirmwares.AsNoTracking()
                .Where(f => f.Board != null && visible.Contains(f.Source))
                .Select(f => new { f.Board, f.Version })
                .ToListAsync();
            var latestPerBoard = catalog
                .GroupBy(f => f.Board!)
                .ToDictionary(g => g.Key, g => g.Select(f => f.Version).Where(FirmwareVersion.IsValid).OrderByDescending(v => FirmwareVersion.Parse(v!)).FirstOrDefault());

            DateTime utcNow = DateTime.UtcNow;
            return rows.Select(r =>
            {
                string? latest = r.Diag?.Board != null && latestPerBoard.TryGetValue(r.Diag.Board, out var v) ? v : null;
                return new DeviceFleetStatus
                {
                    IDDevice = r.Device.IDDevice,
                    TenantID = r.Device.TenantID,
                    DeviceName = r.Device.DeviceName,
                    Enabled = r.Device.Enabled,
                    SleepSeconds = r.Device.SleepSeconds,
                    LastSeenAt = r.Diag?.LastSeenAt,
                    UptimeSeconds = r.Diag?.UptimeSeconds,
                    RssiDbm = r.Diag?.RssiDbm,
                    FreeHeapBytes = r.Diag?.FreeHeapBytes,
                    MinFreeHeapBytes = r.Diag?.MinFreeHeapBytes,
                    MaxAllocHeapBytes = r.Diag?.MaxAllocHeapBytes,
                    StackHighWaterMarkBytes = r.Diag?.StackHighWaterMarkBytes,
                    NetworkStackHighWaterMarkBytes = r.Diag?.NetworkStackHighWaterMarkBytes,
                    FirmwareVersion = r.Diag?.FirmwareVersion,
                    Board = r.Diag?.Board,
                    Kit = r.Diag?.DeviceTypeID is int diagTypeId && deviceTypesById.TryGetValue(diagTypeId, out var diagType) ? diagType.Kit : null,
                    // Admin's explicit DeviceControllerEnabled choice always wins if set - a recognized DeviceType only adds capability, never takes it away. ManualDeviceTypeID is the fallback for a device whose firmware never auto-reports one; the diagnostic-reported DeviceTypeID takes priority whenever both are set.
                    ControllerCapable = r.Device.DeviceControllerEnabled == true
                        || ((r.Diag?.DeviceTypeID ?? r.Device.ManualDeviceTypeID) is int effectiveTypeId && deviceTypesById.TryGetValue(effectiveTypeId, out var effectiveType) && effectiveType.ControllerCapable),
                    LatestFirmwareVersion = latest,
                    FirmwareUpdateAvailable = FirmwareVersion.IsNewer(latest, r.Diag?.FirmwareVersion),
                    FirmwareUpdatePending = r.Device.FirmwareUpdate == true,
                    FirmwareTargetVersion = r.Device.FirmwareTargetVersion,
                    Battery = r.Battery,
                    BatteryEnabled = r.Device.BatteryEnabled,
                    Online = DeviceFleetStatus.ComputeOnline(r.Diag?.LastSeenAt, r.Device.SleepSeconds, utcNow),
                    IsVirtual = virtualDeviceIds.Contains(r.Device.IDDevice),
                    DeviceFarmUnitID = r.Device.DeviceFarmUnitID,
                    DeviceFarmUnitZoneID = r.Device.DeviceFarmUnitZoneID,
                    DeviceFarmUnitName = r.Device.DeviceFarmUnitID is int uid ? unitNames.GetValueOrDefault(uid) : null,
                    DeviceFarmUnitZoneName = r.Device.DeviceFarmUnitZoneID is int zid ? zoneNames.GetValueOrDefault(zid) : null,
                    SowingID = r.Device.SowingID,
                    FarmParcelZoneID = r.Device.FarmParcelZoneID,
                    FarmName = FarmNameFor(r.Device),
                    RelayStates = relayStates.GetValueOrDefault(r.Device.IDDevice),
                };
            }).ToList();
        }
    }
}
