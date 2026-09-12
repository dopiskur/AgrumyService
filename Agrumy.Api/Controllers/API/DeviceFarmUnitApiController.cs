using Agrumy.Shared;
using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Rules;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Controllers.API
{
    /// Unit/Zone CRUD, device assignment, and hierarchical dashboard aggregation - ownership checks mirror DeviceApiController.EnsureOwnedDeviceAsync, same CallerReadsDevicesGlobally/CallerManagesDevicesGlobally rules as the rest of the Device domain.
    [Route("/api/DeviceFarmUnit")]
    public partial class DeviceFarmUnitApiController(IDeviceFarmUnitRepository deviceFarmUnitRepo, ISowingRepository sowingRepo, IFarmParcelRepository farmParcelRepo, IZonePlantingRepository zonePlantingRepo, ICropCatalogRepository cropCatalogRepo, IFieldLogRepository fieldLogRepo, IDeviceRepository deviceRepo, IServerConfigRepository serverConfigRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, IOptions<AgrumySettings> settingsOptions, ManualActuateService manualActuate, DeviceOutboxService commandQueue, Agrumy.Api.Quota.TenantQuotaEnforcer quotaEnforcer, Agrumy.Api.Devices.RuleValidationService ruleValidation, Agrumy.Api.Devices.RuleScopeConflictService ruleScopeConflict, IHorticultureCatalogRepository horticultureCatalogRepo) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        // Absolute ceiling - must match AgrumyFirmware DeviceModel.h's MAX_RULES, enforced independently of ServerConfig.MaxRulesPerZone in case a row predates that validation.
        private const int HardMaxRulesPerZone = 32;

        [Authorize]
        [HttpGet("Farm/All")]
        public async Task<ActionResult<IList<DeviceFarm>>> DeviceFarmsGet() =>
            Ok(await deviceFarmUnitRepo.DeviceFarmsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        [Authorize]
        [HttpGet("Farm")]
        public async Task<ActionResult<DeviceFarm>> DeviceFarmGet(int? idDeviceFarm)
        {
            var (farm, error) = await EnsureOwnedFarmAsync(idDeviceFarm, forWrite: false);
            return error ?? Ok(farm);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Farm")]
        public async Task<ActionResult<DeviceFarm>> DeviceFarmAdd([FromBody] DeviceFarm farm)
        {
            farm.TenantID = CallerTenantId; // payload cannot pick another tenant - same rule as every other Add
            DeviceFarm added;
            try
            {
                added = await deviceFarmUnitRepo.DeviceFarmAddAsync(farm, () => quotaEnforcer.CheckCanAddFarmAsync(farm.TenantID));
            }
            catch (QuotaLimitExceededException ex)
            {
                return ForbidWith(ex.Message);
            }
            await WriteAuditAsync("DeviceFarm.Created", added.TenantID, "DeviceFarm", added.IDDeviceFarm.ToString()!, added.DeviceFarmName);
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut("Farm")]
        public async Task<ActionResult<bool>> DeviceFarmUpdate([FromBody] DeviceFarm farm)
        {
            var (existing, error) = await EnsureOwnedFarmAsync(farm.IDDeviceFarm, forWrite: true);
            if (error != null)
            {
                return error;
            }
            farm.TenantID = existing!.TenantID; // payload cannot move a farm to another tenant
            await deviceFarmUnitRepo.DeviceFarmUpdateAsync(farm);
            await WriteAuditAsync("DeviceFarm.Updated", existing.TenantID, "DeviceFarm", existing.IDDeviceFarm.ToString()!, farm.DeviceFarmName);
            return true;
        }

        /// The Farms page's drag-and-drop card order - full replacement of every listed farm's DisplayOrder by index, not a partial patch. Every id must resolve to an owned farm AND belong to the same tenant, or the whole request is rejected.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Farm/Reorder")]
        public async Task<ActionResult<bool>> DeviceFarmsReorder([FromBody] List<int> orderedFarmIds)
        {
            if (orderedFarmIds is null or [])
            {
                return BadRequest("orderedFarmIds is required.");
            }

            int? tenantId = null;
            foreach (int id in orderedFarmIds)
            {
                var (farm, error) = await EnsureOwnedFarmAsync(id, forWrite: true);
                if (error != null)
                {
                    return error;
                }
                tenantId ??= farm!.TenantID;
                if (farm!.TenantID != tenantId)
                {
                    return BadRequest("All farms in one reorder request must belong to the same tenant.");
                }
            }

            await deviceFarmUnitRepo.DeviceFarmsReorderAsync(tenantId!.Value, orderedFarmIds);
            await WriteAuditAsync("DeviceFarm.Reordered", tenantId, "DeviceFarm", string.Join(",", orderedFarmIds), null);
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Farm")]
        public async Task<ActionResult<bool>> DeviceFarmDelete(int? idDeviceFarm)
        {
            var (farm, error) = await EnsureOwnedFarmAsync(idDeviceFarm, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await deviceFarmUnitRepo.DeviceFarmDeleteAsync(farm!.IDDeviceFarm!.Value);
            await WriteAuditAsync("DeviceFarm.Deleted", farm.TenantID, "DeviceFarm", idDeviceFarm.ToString()!, farm.DeviceFarmName);
            return true;
        }

        [Authorize]
        [HttpGet("All")]
        public async Task<ActionResult<IList<DeviceFarmUnit>>> DeviceFarmUnitsGet() =>
            Ok(await deviceFarmUnitRepo.DeviceFarmUnitsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<DeviceFarmUnit>> DeviceFarmUnitGet(int? idDeviceFarmUnit)
        {
            var (unit, error) = await EnsureOwnedUnitAsync(idDeviceFarmUnit, forWrite: false);
            return error ?? Ok(unit);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        public async Task<ActionResult<DeviceFarmUnit>> DeviceFarmUnitAdd([FromBody] DeviceFarmUnit unit)
        {
            unit.TenantID = CallerTenantId; // payload cannot pick another tenant - same rule as every other Add
            DeviceFarmUnit added;
            try
            {
                added = await deviceFarmUnitRepo.DeviceFarmUnitAddAsync(unit, () => quotaEnforcer.CheckCanAddUnitAsync(unit.TenantID));
            }
            catch (QuotaLimitExceededException ex)
            {
                return ForbidWith(ex.Message);
            }
            await WriteAuditAsync("DeviceFarmUnit.Created", added.TenantID, "DeviceFarmUnit", added.IDDeviceFarmUnit.ToString()!, added.DeviceFarmUnitName);
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPut]
        public async Task<ActionResult<bool>> DeviceFarmUnitUpdate([FromBody] DeviceFarmUnit unit)
        {
            var (existing, error) = await EnsureOwnedUnitAsync(unit.IDDeviceFarmUnit, forWrite: true);
            if (error != null)
            {
                return error;
            }
            unit.TenantID = existing!.TenantID; // payload cannot move a unit to another tenant
            await deviceFarmUnitRepo.DeviceFarmUnitUpdateAsync(unit);
            await WriteAuditAsync("DeviceFarmUnit.Updated", existing.TenantID, "DeviceFarmUnit", existing.IDDeviceFarmUnit.ToString()!, unit.DeviceFarmUnitName);
            return true;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete]
        public async Task<ActionResult<bool>> DeviceFarmUnitDelete(int? idDeviceFarmUnit)
        {
            var (unit, error) = await EnsureOwnedUnitAsync(idDeviceFarmUnit, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await deviceFarmUnitRepo.DeviceFarmUnitDeleteAsync(unit!.IDDeviceFarmUnit!.Value);
            await WriteAuditAsync("DeviceFarmUnit.Deleted", unit.TenantID, "DeviceFarmUnit", idDeviceFarmUnit.ToString()!, unit.DeviceFarmUnitName);
            return true;
        }

        /// The Farms page's drag-and-drop unit cube order, same convention as DeviceFarmsReorder - full replacement of every listed unit's DisplayOrder by index. Every id must resolve to an owned unit AND belong to the same tenant, or the whole request is rejected.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Reorder")]
        public async Task<ActionResult<bool>> DeviceFarmUnitsReorder([FromBody] List<int> orderedUnitIds)
        {
            if (orderedUnitIds is null or [])
            {
                return BadRequest("orderedUnitIds is required.");
            }

            int? tenantId = null;
            foreach (int id in orderedUnitIds)
            {
                var (unit, error) = await EnsureOwnedUnitAsync(id, forWrite: true);
                if (error != null)
                {
                    return error;
                }
                tenantId ??= unit!.TenantID;
                if (unit!.TenantID != tenantId)
                {
                    return BadRequest("All units in one reorder request must belong to the same tenant.");
                }
            }

            await deviceFarmUnitRepo.DeviceFarmUnitsReorderAsync(tenantId!.Value, orderedUnitIds);
            await WriteAuditAsync("DeviceFarmUnit.Reordered", tenantId, "DeviceFarmUnit", string.Join(",", orderedUnitIds), null);
            return true;
        }

        /// Reuses the per-device IssueWifiUpdateCommandAsync (verify-then-persist runs on the device itself, unchanged) across every device under the unit; a device that already has one pending is skipped, not retried.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("{idDeviceFarmUnit}/WifiUpdate")]
        public async Task<ActionResult<UnitWifiUpdateResult>> UnitWifiUpdate(int idDeviceFarmUnit, [FromBody] UnitWifiUpdateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Ssid))
            {
                return BadRequest("Ssid is required.");
            }
            var (unit, error) = await EnsureOwnedUnitAsync(idDeviceFarmUnit, forWrite: true);
            if (error != null)
            {
                return error;
            }

            IList<Device> devices = await deviceFarmUnitRepo.DeviceFarmUnitGetDevicesAsync(unit!.IDDeviceFarmUnit!.Value);
            var result = new UnitWifiUpdateResult { DeviceCount = devices.Count };
            foreach (Device device in devices)
            {
                if (device.IDDevice is not int deviceId)
                {
                    continue;
                }
                IssueCommandResult issued = await commandQueue.IssueWifiUpdateCommandAsync(deviceId, request.Ssid, request.WifiPassword);
                bool success = issued.Outcome == IssueCommandOutcome.Success;
                if (success)
                {
                    result.IssuedCount++;
                }
                result.Devices.Add(new UnitWifiUpdateDeviceResult
                {
                    IDDevice = deviceId,
                    DeviceName = device.DeviceName,
                    Issued = success,
                    Message = success ? null : issued.Message,
                });
            }
            await WriteAuditAsync("DeviceFarmUnit.WifiUpdateIssued", unit.TenantID, "DeviceFarmUnit", idDeviceFarmUnit.ToString(), $"{result.IssuedCount}/{result.DeviceCount} devices");
            return Ok(result);
        }

        /// Same shape as EnsureOwnedUnitAsync, for DeviceFarmUnitZone.
        private Task<OwnedResult<DeviceFarmUnitZone>> EnsureOwnedZoneAsync(int? idDeviceFarmUnitZone, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmUnitZoneGetByIdAsync(idDeviceFarmUnitZone), z => z.TenantID, "Zone", forWrite);

        /// Routes a dashboard widget's own (level, levelId) target through whichever EnsureOwned*Async matches its level - a widget's data source is checked independently of the zone whose page it happens to be displayed on.
        private async Task<ActionResult?> EnsureOwnedAggregationTargetAsync(HierarchyNodeKind level, int levelId, bool forWrite) => level switch
        {
            HierarchyNodeKind.Farm => (await EnsureOwnedFarmAsync(levelId, forWrite)).Error,
            HierarchyNodeKind.Unit => (await EnsureOwnedUnitAsync(levelId, forWrite)).Error,
            HierarchyNodeKind.Zone => (await EnsureOwnedZoneAsync(levelId, forWrite)).Error,
            HierarchyNodeKind.Sowing => (await EnsureOwnedCropAsync(levelId, forWrite)).Error,
            HierarchyNodeKind.FarmParcelZone => (await EnsureOwnedParcelAsync(levelId, forWrite)).Error,
            _ => BadRequest("Unknown dashboard aggregation level."),
        };

        /// Open-Field's mid-level equivalent of EnsureOwnedUnitAsync.
        private Task<OwnedResult<Sowing>> EnsureOwnedCropAsync(int? idSowing, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => sowingRepo.SowingGetByIdAsync(idSowing ?? 0), c => c.TenantID, "Crop", forWrite);

        /// Open-Field's leaf-level equivalent of EnsureOwnedZoneAsync.
        private Task<OwnedResult<FarmParcelZone>> EnsureOwnedParcelAsync(int? idFarmParcelZone, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => farmParcelRepo.FarmParcelZoneGetByIdAsync(idFarmParcelZone ?? 0), p => p.TenantID, "Parcel", forWrite);

        /// Same shape as EnsureOwnedUnitAsync, for Device.
        private Task<OwnedResult<Device>> EnsureOwnedDeviceAsync(
            Func<Task<Device?>> lookup, string ownerLabel, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(lookup, d => d.TenantID, ownerLabel, forWrite);
    }
}
