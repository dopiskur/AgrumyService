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

        public async Task DeviceAssignToZoneAsync(int idDevice, int idDeviceFarmUnitZone)
        {
            var zone = await db.DeviceFarmUnitZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
            var device = await db.Devices.FirstOrDefaultAsync(d => d.IDDevice == idDevice);
            if (zone == null || device == null)
            {
                return;
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
