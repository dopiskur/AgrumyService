using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Open-Field's Crop/Parcel CRUD, device assignment, and Farm-with-extension creation - the Open-Field mirror of DeviceFarmUnitApiController's Unit/Zone CRUD. Farm-level CRUD/reorder/delete/recycle-bin stays on DeviceFarmUnitApiController (shared by both branches); this controller only owns what's genuinely new. DeviceUnassignedGetAsync is likewise reused from there rather than duplicated - it already excludes both branches' assigned devices.
    [Route("/api/FarmOpenfield")]
    public class FarmOpenfieldApiController(IFarmOpenfieldRepository farmOpenfieldRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IDeviceRepository deviceRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, Agrumy.Api.Quota.TenantQuotaEnforcer quotaEnforcer) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        #region Farm-with-extension creation

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        public async Task<ActionResult<DeviceFarm>> FarmOpenfieldCreate([FromBody] string? farmName)
        {
            (DeviceFarm farm, FarmOpenfield openfield) result;
            try
            {
                result = await farmOpenfieldRepo.FarmOpenfieldCreateAsync(farmName, CallerTenantId, () => quotaEnforcer.CheckCanAddFarmAsync(CallerTenantId));
            }
            catch (QuotaLimitExceededException ex)
            {
                return StatusCode(403, ex.Message);
            }
            await WriteAuditAsync("DeviceFarm.Created", result.farm.TenantID, "DeviceFarm", result.farm.IDDeviceFarm.ToString()!, $"{result.farm.DeviceFarmName} (Open-Field)");
            return Ok(result.farm);
        }

        [Authorize]
        [HttpGet("All")]
        public async Task<ActionResult<IList<FarmOpenfield>>> FarmOpenfieldsGet() =>
            Ok(await farmOpenfieldRepo.FarmOpenfieldsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        #endregion

        #region Crop CRUD

        [Authorize]
        [HttpGet("Crop/All")]
        public async Task<ActionResult<IList<FarmOpenfieldCrop>>> CropsGet() =>
            Ok(await farmOpenfieldRepo.CropsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        [Authorize]
        [HttpGet("Crop")]
        public async Task<ActionResult<FarmOpenfieldCrop>> CropGet(int? idFarmOpenfieldCrop)
        {
            var (crop, error) = await EnsureOwnedCropAsync(idFarmOpenfieldCrop, forWrite: false);
            return error ?? Ok(crop);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Crop")]
        public async Task<ActionResult<FarmOpenfieldCrop>> CropAdd([FromBody] FarmOpenfieldCrop crop)
        {
            var (openfield, farm, error) = await EnsureOwnedOpenfieldAsync(crop.FarmOpenfieldID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            crop.TenantID = farm!.TenantID;
            FarmOpenfieldCrop added;
            try
            {
                added = await farmOpenfieldRepo.CropAddAsync(crop, () => quotaEnforcer.CheckCanAddCropAsync(crop.TenantID));
            }
            catch (QuotaLimitExceededException ex)
            {
                return StatusCode(403, ex.Message);
            }
            await WriteAuditAsync("FarmOpenfieldCrop.Created", added.TenantID, "FarmOpenfieldCrop", added.IDFarmOpenfieldCrop.ToString()!, added.FarmOpenfieldCropName);
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Crop")]
        public async Task<ActionResult<bool>> CropUpdate([FromBody] FarmOpenfieldCrop crop)
        {
            var (existing, error) = await EnsureOwnedCropAsync(crop.IDFarmOpenfieldCrop, forWrite: true);
            if (error != null)
            {
                return error;
            }
            crop.TenantID = existing!.TenantID;
            await farmOpenfieldRepo.CropUpdateAsync(crop);
            await WriteAuditAsync("FarmOpenfieldCrop.Updated", existing.TenantID, "FarmOpenfieldCrop", existing.IDFarmOpenfieldCrop.ToString()!, crop.FarmOpenfieldCropName);
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Crop/Reorder")]
        public async Task<ActionResult<bool>> CropsReorder([FromBody] List<int> orderedCropIds)
        {
            if (orderedCropIds is null or [])
            {
                return BadRequest("orderedCropIds is required.");
            }
            int? tenantId = null;
            foreach (int id in orderedCropIds)
            {
                var (crop, error) = await EnsureOwnedCropAsync(id, forWrite: true);
                if (error != null)
                {
                    return error;
                }
                tenantId ??= crop!.TenantID;
                if (crop!.TenantID != tenantId)
                {
                    return BadRequest("All crops in one reorder request must belong to the same tenant.");
                }
            }
            await farmOpenfieldRepo.CropsReorderAsync(tenantId!.Value, orderedCropIds);
            await WriteAuditAsync("FarmOpenfieldCrop.Reordered", tenantId, "FarmOpenfieldCrop", string.Join(",", orderedCropIds), null);
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Crop")]
        public async Task<ActionResult<bool>> CropDelete(int? idFarmOpenfieldCrop)
        {
            var (crop, error) = await EnsureOwnedCropAsync(idFarmOpenfieldCrop, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await farmOpenfieldRepo.CropDeleteAsync(crop!.IDFarmOpenfieldCrop!.Value);
            await WriteAuditAsync("FarmOpenfieldCrop.Deleted", crop.TenantID, "FarmOpenfieldCrop", idFarmOpenfieldCrop.ToString()!, crop.FarmOpenfieldCropName);
            return true;
        }

        #endregion

        #region Parcel CRUD

        [Authorize]
        [HttpGet("Parcel")]
        public async Task<ActionResult<IList<FarmOpenfieldCropParcel>>> ParcelsGet(int? idFarmOpenfieldCrop)
        {
            var (crop, error) = await EnsureOwnedCropAsync(idFarmOpenfieldCrop, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await farmOpenfieldRepo.ParcelsGetAsync(crop!.IDFarmOpenfieldCrop!.Value));
        }

        [Authorize]
        [HttpGet("ParcelById")]
        public async Task<ActionResult<FarmOpenfieldCropParcel>> ParcelGetById(int? idFarmOpenfieldCropParcel)
        {
            var (parcel, error) = await EnsureOwnedParcelAsync(idFarmOpenfieldCropParcel, forWrite: false);
            return error ?? Ok(parcel);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Parcel")]
        public async Task<ActionResult<FarmOpenfieldCropParcel>> ParcelAdd([FromBody] FarmOpenfieldCropParcel parcel)
        {
            var (crop, error) = await EnsureOwnedCropAsync(parcel.FarmOpenfieldCropID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            parcel.TenantID = crop!.TenantID;
            FarmOpenfieldCropParcel added;
            try
            {
                added = await farmOpenfieldRepo.ParcelAddAsync(parcel, () => quotaEnforcer.CheckCanAddParcelAsync(parcel.TenantID));
            }
            catch (QuotaLimitExceededException ex)
            {
                return StatusCode(403, ex.Message);
            }
            await WriteAuditAsync("FarmOpenfieldCropParcel.Created", added.TenantID, "FarmOpenfieldCropParcel", added.IDFarmOpenfieldCropParcel.ToString()!, added.FarmOpenfieldCropParcelName);
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Parcel")]
        public async Task<ActionResult<bool>> ParcelUpdate([FromBody] FarmOpenfieldCropParcel parcel)
        {
            var (existing, error) = await EnsureOwnedParcelAsync(parcel.IDFarmOpenfieldCropParcel, forWrite: true);
            if (error != null)
            {
                return error;
            }

            // Same server-side sanity check as DeviceFarmUnitApiController.DeviceFarmUnitZoneUpdate.
            if (!SafetyLimitValidation.IsValid(parcel.WaterPumpMaxRunSeconds))
            {
                return BadRequest($"WaterPump max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(parcel.WaterPumpCooldownSeconds))
            {
                return BadRequest($"WaterPump cooldown must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(parcel.HeatingMaxRunSeconds))
            {
                return BadRequest($"Heating max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(parcel.VentilationMaxRunSeconds))
            {
                return BadRequest($"Ventilation max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }

            parcel.TenantID = existing!.TenantID;
            parcel.FarmOpenfieldCropID = existing.FarmOpenfieldCropID; // rename only - see ParcelMigrate for the deliberate move
            await farmOpenfieldRepo.ParcelUpdateAsync(parcel);
            await WriteAuditAsync("FarmOpenfieldCropParcel.Updated", existing.TenantID, "FarmOpenfieldCropParcel", existing.IDFarmOpenfieldCropParcel.ToString()!, parcel.FarmOpenfieldCropParcelName);
            return true;
        }

        /// Deliberate crop reassignment, same shape as DeviceFarmUnitApiController.DeviceFarmUnitZoneMigrate.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Parcel/{idFarmOpenfieldCropParcel}/Migrate")]
        public async Task<ActionResult<bool>> ParcelMigrate(int idFarmOpenfieldCropParcel, int idTargetFarmOpenfieldCrop)
        {
            var (parcel, parcelError) = await EnsureOwnedParcelAsync(idFarmOpenfieldCropParcel, forWrite: true);
            if (parcelError != null)
            {
                return parcelError;
            }
            var (targetCrop, cropError) = await EnsureOwnedCropAsync(idTargetFarmOpenfieldCrop, forWrite: true);
            if (cropError != null)
            {
                return cropError;
            }
            if (targetCrop!.TenantID != parcel!.TenantID)
            {
                return BadRequest("Target crop belongs to a different tenant.");
            }
            if (targetCrop.IDFarmOpenfieldCrop == parcel.FarmOpenfieldCropID)
            {
                return BadRequest("Parcel is already in that crop.");
            }

            string fromCropName = (await farmOpenfieldRepo.CropGetByIdAsync(parcel.FarmOpenfieldCropID))?.FarmOpenfieldCropName ?? parcel.FarmOpenfieldCropID.ToString();
            await farmOpenfieldRepo.ParcelMigrateAsync(parcel.IDFarmOpenfieldCropParcel!.Value, targetCrop.IDFarmOpenfieldCrop!.Value);
            await WriteAuditAsync("FarmOpenfieldCropParcel.Migrated", parcel.TenantID, "FarmOpenfieldCropParcel", parcel.IDFarmOpenfieldCropParcel.ToString()!, $"{parcel.FarmOpenfieldCropParcelName}: {fromCropName} -> {targetCrop.FarmOpenfieldCropName}");
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Parcel")]
        public async Task<ActionResult<bool>> ParcelDelete(int? idFarmOpenfieldCropParcel)
        {
            var (parcel, error) = await EnsureOwnedParcelAsync(idFarmOpenfieldCropParcel, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await farmOpenfieldRepo.ParcelDeleteAsync(parcel!.IDFarmOpenfieldCropParcel!.Value);
            await WriteAuditAsync("FarmOpenfieldCropParcel.Deleted", parcel.TenantID, "FarmOpenfieldCropParcel", idFarmOpenfieldCropParcel.ToString()!, parcel.FarmOpenfieldCropParcelName);
            return true;
        }

        #endregion

        #region Device assignment

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Assign")]
        public async Task<ActionResult<bool>> DeviceAssign([FromBody] DeviceParcelAssignment body)
        {
            var (device, deviceError) = await EnsureOwnedDeviceAsync(() => deviceRepo.DeviceGetByIdAsync(body.IDDevice), "Device", forWrite: true);
            if (deviceError != null)
            {
                return deviceError;
            }
            var (parcel, parcelError) = await EnsureOwnedParcelAsync(body.IDFarmOpenfieldCropParcel, forWrite: true);
            if (parcelError != null)
            {
                return parcelError;
            }
            if (device!.TenantID != parcel!.TenantID)
            {
                return StatusCode(403, "Device and parcel belong to different tenants.");
            }
            if (device.DeviceControllerEnabled == true && await farmOpenfieldRepo.ParcelHasControllerAsync(body.IDFarmOpenfieldCropParcel))
            {
                return Conflict("This parcel already has a controller assigned.");
            }

            await farmOpenfieldRepo.DeviceAssignToParcelAsync(body.IDDevice, body.IDFarmOpenfieldCropParcel);
            await WriteAuditAsync("Device.AssignedToParcel", device.TenantID, "Device", body.IDDevice.ToString(), $"parcel {body.IDFarmOpenfieldCropParcel}");
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Unassign")]
        public async Task<ActionResult<bool>> DeviceUnassign(int? idDevice)
        {
            var (device, error) = await EnsureOwnedDeviceAsync(() => deviceRepo.DeviceGetByIdAsync(idDevice), "Device", forWrite: true);
            if (error != null)
            {
                return error;
            }
            await farmOpenfieldRepo.DeviceUnassignFromParcelAsync(device!.IDDevice!.Value);
            await WriteAuditAsync("Device.UnassignedFromParcel", device.TenantID, "Device", idDevice.ToString()!, null);
            return true;
        }

        #endregion

        private Task<OwnedResult<FarmOpenfieldCrop>> EnsureOwnedCropAsync(int? idFarmOpenfieldCrop, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => farmOpenfieldRepo.CropGetByIdAsync(idFarmOpenfieldCrop), c => c.TenantID, "Crop", forWrite);

        private Task<OwnedResult<FarmOpenfieldCropParcel>> EnsureOwnedParcelAsync(int? idFarmOpenfieldCropParcel, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => farmOpenfieldRepo.ParcelGetByIdAsync(idFarmOpenfieldCropParcel), p => p.TenantID, "Parcel", forWrite);

        private Task<OwnedResult<Device>> EnsureOwnedDeviceAsync(Func<Task<Device?>> lookup, string ownerLabel, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(lookup, d => d.TenantID, ownerLabel, forWrite);

        /// Resolves the owning Farm (for its TenantID) from a FarmOpenfieldID - CropAdd's ownership check is really "does the caller own the Farm this Open-Field extension belongs to". No direct "get FarmOpenfield by its own id" repository lookup exists (FarmOpenfieldGetByFarmIdAsync is keyed by FarmID, the FK direction FarmOpenfieldCreateAsync needs), so this scans FarmOpenfieldsGetAsync's small admin-managed set instead of adding a second lookup shape for one caller.
        private async Task<(FarmOpenfield? Openfield, DeviceFarm? Farm, ActionResult? Error)> EnsureOwnedOpenfieldAsync(int idFarmOpenfield, bool forWrite)
        {
            IList<FarmOpenfield> openfields = await farmOpenfieldRepo.FarmOpenfieldsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId);
            FarmOpenfield? openfield = openfields.FirstOrDefault(o => o.IDFarmOpenfield == idFarmOpenfield);
            if (openfield == null)
            {
                return (null, null, NotFound());
            }
            var (farm, error) = await EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmGetByIdAsync(openfield.FarmID), f => f.TenantID, "Farm", forWrite);
            return (openfield, farm, error);
        }
    }
}
