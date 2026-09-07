using api.Dal.Interface;
using api.Models;
using api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.API
{
    /// Roadmap #409 - lists and restores soft-deleted Farms/Devices (see AgrumyDbContext's HasQueryFilter on each). Actual deletion still happens through the existing DeviceApiController.DeviceDelete / DeviceFarmUnitApiController.DeviceFarmDelete endpoints - this controller only reads the recycle bin and undoes it.
    [Route("/api/RecycleBin")]
    public class RecycleBinApiController(IDeviceRepository deviceRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpGet("Device")]
        public async Task<ActionResult<IList<DeviceDto>>> DevicesGet() =>
            Ok((await deviceRepo.DeviceRecycleBinGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId)).Select(d => d.ToDto()).ToList());

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpGet("Farm")]
        public async Task<ActionResult<IList<DeviceFarm>>> FarmsGet() =>
            Ok(await deviceFarmUnitRepo.DeviceFarmRecycleBinGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Device/{idDevice}/Restore")]
        public async Task<ActionResult<bool>> DeviceRestore(int idDevice)
        {
            Device? device = await deviceRepo.DeviceRecycleBinGetByIdAsync(idDevice);
            if (device is null)
            {
                return NotFound();
            }
            if (!CallerManagesDevices(device.TenantID))
            {
                return StatusCode(403, "Device belongs to a different tenant");
            }

            bool restored = await deviceRepo.DeviceRestoreAsync(idDevice, device.TenantID);
            if (restored)
            {
                await WriteAuditAsync("Device.Restored", device.TenantID, "Device", idDevice.ToString(), device.DeviceName);
            }
            return restored;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Farm/{idDeviceFarm}/Restore")]
        public async Task<ActionResult<bool>> FarmRestore(int idDeviceFarm)
        {
            DeviceFarm? farm = await deviceFarmUnitRepo.DeviceFarmRecycleBinGetByIdAsync(idDeviceFarm);
            if (farm is null)
            {
                return NotFound();
            }
            if (!CallerManagesDevices(farm.TenantID))
            {
                return StatusCode(403, "Farm belongs to a different tenant");
            }

            bool restored = await deviceFarmUnitRepo.DeviceFarmRestoreAsync(idDeviceFarm, farm.TenantID);
            if (restored)
            {
                await WriteAuditAsync("DeviceFarm.Restored", farm.TenantID, "DeviceFarm", idDeviceFarm.ToString(), farm.DeviceFarmName);
            }
            return restored;
        }
    }
}
