using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Roadmap #409/#427 - lists, restores, and marks-for-purge soft-deleted Farms/Devices (see AgrumyDbContext's HasQueryFilter on each). The actual irreversible removal never happens here - it's PurgeOrphanedSensorDataEvaluator's reap phase, scheduled or manually forced via DataMaintenanceApiController.PurgeOrphaned.
    [Route("/api/RecycleBin")]
    public class RecycleBinApiController(IDeviceRepository deviceRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("Device")]
        public async Task<ActionResult<IList<DeviceDto>>> DevicesGet() =>
            Ok((await deviceRepo.DeviceRecycleBinGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId)).Select(d => d.ToDto()).ToList());

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("Farm")]
        public async Task<ActionResult<IList<DeviceFarm>>> FarmsGet() =>
            Ok(await deviceFarmUnitRepo.DeviceFarmRecycleBinGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        /// Roadmap #427 - devices marked for permanent removal but not yet reaped; still restorable via DeviceRestore.
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("Device/PendingPurge")]
        public async Task<ActionResult<IList<DeviceDto>>> DevicesPendingPurgeGet() =>
            Ok((await deviceRepo.DevicePendingPurgeGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId)).Select(d => d.ToDto()).ToList());

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("Farm/PendingPurge")]
        public async Task<ActionResult<IList<DeviceFarm>>> FarmsPendingPurgeGet() =>
            Ok(await deviceFarmUnitRepo.DeviceFarmPendingPurgeGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

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

        /// Roadmap #427 - Global Admin-only, confirmation-phrase-gated, same bar as DataMaintenanceApiController's manual purge trigger. Only MARKS the device for permanent removal - it stays fully restorable via DeviceRestore until the purge cycle actually reaps it (PurgeOrphanedSensorDataEvaluator).
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost("Device/{idDevice}/PurgeNow")]
        public async Task<ActionResult<bool>> DevicePurgeNow(int idDevice, [FromBody] RecycleBinPurgeRequest value)
        {
            if (value.ConfirmationPhrase != RecycleBinPurgeRequest.RequiredPhrase)
            {
                return BadRequest($"Type \"{RecycleBinPurgeRequest.RequiredPhrase}\" to confirm this destructive action.");
            }

            Device? device = await deviceRepo.DeviceRecycleBinGetByIdAsync(idDevice);
            if (device is null)
            {
                return NotFound();
            }

            bool marked = await deviceRepo.DeviceRecycleBinMarkPurgedAsync(idDevice, device.TenantID);
            if (marked)
            {
                await WriteAuditAsync("Device.MarkedForPurge", device.TenantID, "Device", idDevice.ToString(), device.DeviceName);
            }
            return marked;
        }

        /// Same bar as DevicePurgeNow, for a Farm and its exact soft-delete cascade.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost("Farm/{idDeviceFarm}/PurgeNow")]
        public async Task<ActionResult<bool>> FarmPurgeNow(int idDeviceFarm, [FromBody] RecycleBinPurgeRequest value)
        {
            if (value.ConfirmationPhrase != RecycleBinPurgeRequest.RequiredPhrase)
            {
                return BadRequest($"Type \"{RecycleBinPurgeRequest.RequiredPhrase}\" to confirm this destructive action.");
            }

            DeviceFarm? farm = await deviceFarmUnitRepo.DeviceFarmRecycleBinGetByIdAsync(idDeviceFarm);
            if (farm is null)
            {
                return NotFound();
            }

            bool marked = await deviceFarmUnitRepo.DeviceFarmRecycleBinMarkPurgedAsync(idDeviceFarm, farm.TenantID);
            if (marked)
            {
                await WriteAuditAsync("DeviceFarm.MarkedForPurge", farm.TenantID, "DeviceFarm", idDeviceFarm.ToString(), farm.DeviceFarmName);
            }
            return marked;
        }

        /// "Empty Recycle Bin" - marks everything currently listed for permanent removal, not just one item. Still fully restorable (via the Pending Purge list) until the purge cycle actually reaps it.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost("Empty")]
        public async Task<ActionResult<RecycleBinEmptyResult>> EmptyRecycleBin([FromBody] RecycleBinPurgeRequest value)
        {
            if (value.ConfirmationPhrase != RecycleBinPurgeRequest.RequiredPhrase)
            {
                return BadRequest($"Type \"{RecycleBinPurgeRequest.RequiredPhrase}\" to confirm this destructive action.");
            }

            int? scopeTenantId = CallerReadsDevicesGlobally ? null : CallerTenantId;
            var result = new RecycleBinEmptyResult();

            foreach (DeviceFarm farm in await deviceFarmUnitRepo.DeviceFarmRecycleBinGetAsync(scopeTenantId))
            {
                if (await deviceFarmUnitRepo.DeviceFarmRecycleBinMarkPurgedAsync(farm.IDDeviceFarm!.Value, farm.TenantID))
                {
                    result.FarmsMarked++;
                }
            }
            foreach (Device device in await deviceRepo.DeviceRecycleBinGetAsync(scopeTenantId))
            {
                if (await deviceRepo.DeviceRecycleBinMarkPurgedAsync(device.IDDevice!.Value, device.TenantID))
                {
                    result.DevicesMarked++;
                }
            }

            if (result.DevicesMarked > 0 || result.FarmsMarked > 0)
            {
                await WriteAuditAsync("RecycleBin.Emptied", scopeTenantId, "RecycleBin", "-", $"{result.DevicesMarked} device(s), {result.FarmsMarked} farm(s) marked for purge");
            }
            return result;
        }
    }
}
