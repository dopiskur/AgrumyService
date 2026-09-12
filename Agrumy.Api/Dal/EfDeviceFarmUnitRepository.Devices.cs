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
        public async Task<bool> DeviceFarmUnitZoneHasControllerAsync(int idDeviceFarmUnitZone)
        {
            return await db.Devices.AsNoTracking()
                .AnyAsync(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone && d.DeviceControllerEnabled == true);
        }

        public async Task<Device?> DeviceFarmUnitZoneGetControllerAsync(int idDeviceFarmUnitZone)
        {
            var row = await db.Devices.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone && d.DeviceControllerEnabled == true);
            return row == null ? null : EfDeviceRepository.ToDto(row);
        }

        public async Task<IList<Device>> DeviceFarmUnitGetControllersAsync(int idDeviceFarmUnit)
        {
            var rows = await db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitID == idDeviceFarmUnit && d.DeviceControllerEnabled == true)
                .ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        public async Task<IList<Device>> DeviceFarmUnitZoneGetSensorsAsync(int idDeviceFarmUnitZone)
        {
            var rows = await db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone && d.DeviceSensorEnabled == true && d.DeviceControllerEnabled != true)
                .ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        public async Task<IList<Device>> DeviceFarmUnitGetSensorsAsync(int idDeviceFarmUnit)
        {
            var rows = await db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitID == idDeviceFarmUnit && d.DeviceSensorEnabled == true && d.DeviceControllerEnabled != true)
                .ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        public async Task<IList<Device>> DeviceFarmGetSensorsAsync(int idDeviceFarm)
        {
            var unitIds = await db.DeviceFarmUnits.AsNoTracking().Where(u => u.DeviceFarmID == idDeviceFarm).Select(u => u.IDDeviceFarmUnit).ToListAsync();
            var rows = await db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitID != null && unitIds.Contains(d.DeviceFarmUnitID.Value) && d.DeviceSensorEnabled == true && d.DeviceControllerEnabled != true)
                .ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        public async Task<IList<Device>> DeviceFarmGetControllersAsync(int idDeviceFarm)
        {
            var unitIds = await db.DeviceFarmUnits.AsNoTracking().Where(u => u.DeviceFarmID == idDeviceFarm).Select(u => u.IDDeviceFarmUnit).ToListAsync();
            var rows = await db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitID != null && unitIds.Contains(d.DeviceFarmUnitID.Value) && d.DeviceControllerEnabled == true)
                .ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        /// Every device under this unit regardless of role or zone assignment - the bulk WiFi switch fans out to all of these, not just controllers/sensors.
        public async Task<IList<Device>> DeviceFarmUnitGetDevicesAsync(int idDeviceFarmUnit)
        {
            var rows = await db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitID == idDeviceFarmUnit)
                .ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        // ---- Device assignment -----------------------------------------

        public async Task<IList<Device>> DeviceUnassignedGetAsync(int? tenantID, bool controllerCapable)
        {
            // Excludes a device already on the Open-Field branch too - a device is assigned to at most one of the two hierarchies at a time.
            IQueryable<DeviceRow> q = db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitZoneID == null && d.FarmParcelZoneID == null);
            if (tenantID != null)
            {
                q = q.Where(d => d.TenantID == tenantID);
            }
            q = controllerCapable
                ? q.Where(d => d.DeviceControllerEnabled == true)
                : q.Where(d => d.DeviceSensorEnabled == true);

            var rows = await q.ToListAsync();
            return rows.Select(EfDeviceRepository.ToDto).ToList();
        }

        // Same retry count/reasoning as EfUserRepository.RegisterUserAsync's own Serializable-transaction-plus-Contention-retry shape.
        private const int MaxContentionRetries = 3;

        /// enforceOneControllerPerZone is opt-in: other callers (device registration's own zone provisioning, test setups, simulation grouping) have never cared about the zone's one-controller cap and freely put more than one controller-enabled device in a zone - only the interactive Assign endpoint claims that cap, so only it pays for the Serializable transaction below.
        public Task<bool> DeviceAssignToZoneAsync(int idDevice, int idDeviceFarmUnitZone, bool enforceOneControllerPerZone = false) =>
            enforceOneControllerPerZone
                ? AssignToZoneEnforcingControllerCapAsync(idDevice, idDeviceFarmUnitZone)
                : AssignToZoneAsync(idDevice, idDeviceFarmUnitZone);

        private async Task<bool> AssignToZoneAsync(int idDevice, int idDeviceFarmUnitZone)
        {
            var zone = await db.DeviceFarmUnitZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.IDDevice == idDevice);
            if (zone == null || device == null)
            {
                return false;
            }

            device.DeviceFarmUnitID = zone.DeviceFarmUnitID;
            device.DeviceFarmUnitZoneID = zone.IDDeviceFarmUnitZone;
            // Mutual exclusivity - a device moving onto the Greenhouse branch can't still be on the Open-Field one (see DeviceUnassignedGetAsync's own filter).
            device.SowingID = null;
            device.FarmParcelZoneID = null;
            // Bumped (unlike Unassign below) - the device learns its new assignment on its next poll.
            device.ConfigVersion = (device.ConfigVersion ?? 0) + 1;
            await db.SaveChangesAsync();
            await outboxRepository.AddOutboxItemAsync(idDevice, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            await deviceRepository.InvalidateFleetCacheAsync(device.TenantID);
            return true;
        }

        private async Task<bool> AssignToZoneEnforcingControllerCapAsync(int idDevice, int idDeviceFarmUnitZone)
        {
            for (int attempt = 1; ; attempt++)
            {
                await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    var zone = await db.DeviceFarmUnitZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
                    var device = await db.Devices.FirstOrDefaultAsync(d => d.IDDevice == idDevice);
                    if (zone == null || device == null)
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }

                    // Re-checked inside this same transaction, not just by the caller beforehand - a plain pre-check would let two concurrent assigns of two different controllers into the same zone both read "zone is free" and both take it.
                    if (device.DeviceControllerEnabled == true && await db.Devices.AsNoTracking()
                            .AnyAsync(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone && d.DeviceControllerEnabled == true && d.IDDevice != idDevice))
                    {
                        await transaction.RollbackAsync();
                        return false;
                    }

                    device.DeviceFarmUnitID = zone.DeviceFarmUnitID;
                    device.DeviceFarmUnitZoneID = zone.IDDeviceFarmUnitZone;
                    device.SowingID = null;
                    device.FarmParcelZoneID = null;
                    device.ConfigVersion = (device.ConfigVersion ?? 0) + 1;
                    await db.SaveChangesAsync();
                    await outboxRepository.AddOutboxItemAsync(idDevice, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
                    await transaction.CommitAsync();
                    await deviceRepository.InvalidateFleetCacheAsync(device.TenantID);
                    return true;
                }
                catch (Exception ex) when (DbExceptionClassifier.Classify(ex) == DbFailureKind.Contention && attempt < MaxContentionRetries)
                {
                    await transaction.RollbackAsync();
                }
            }
        }

        public async Task DeviceUnassignFromZoneAsync(int idDevice)
        {
            int? tenantID = await db.Devices.AsNoTracking()
                .Where(d => d.IDDevice == idDevice).Select(d => (int?)d.TenantID).FirstOrDefaultAsync();
            // No ConfigVersion bump - the device is not notified, it just stops counting toward any zone's aggregation.
            await db.Devices.Where(d => d.IDDevice == idDevice)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.DeviceFarmUnitID, (int?)null)
                    .SetProperty(d => d.DeviceFarmUnitZoneID, (int?)null));
            await deviceRepository.InvalidateFleetCacheAsync(tenantID);
        }
    }
}
