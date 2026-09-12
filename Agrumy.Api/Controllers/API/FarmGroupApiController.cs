using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// FarmGroup CRUD and farm membership - a cross-cutting overview grouping ABOVE Farm, spanning FarmType. Ownership checks mirror DeviceFarmUnitApiController's own EnsureOwnedFarmAsync.
    [Route("/api/FarmGroup")]
    public class FarmGroupApiController(IFarmGroupRepository farmGroupRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        [Authorize]
        [HttpGet("All")]
        public async Task<ActionResult<IList<FarmGroup>>> FarmGroupsGet() =>
            Ok(await farmGroupRepo.FarmGroupsGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        public async Task<ActionResult<FarmGroup>> FarmGroupCreate([FromBody] string? name)
        {
            FarmGroup added = await farmGroupRepo.FarmGroupCreateAsync(name, CallerTenantId);
            await WriteAuditAsync("FarmGroup.Created", added.TenantID, "FarmGroup", added.IDFarmGroup.ToString()!, added.Name);
            return Ok(added);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete]
        public async Task<ActionResult<bool>> FarmGroupDelete(int idFarmGroup)
        {
            var (group, error) = await EnsureOwnedFarmGroupAsync(idFarmGroup, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await farmGroupRepo.FarmGroupDeleteAsync(idFarmGroup);
            await WriteAuditAsync("FarmGroup.Deleted", group!.TenantID, "FarmGroup", idFarmGroup.ToString(), group.Name);
            return true;
        }

        /// idFarmGroup null unassigns the farm from whatever group it's currently in - at most one group per farm, so this always replaces rather than adds.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("AssignFarm")]
        public async Task<ActionResult<bool>> FarmAssignToGroup(int idFarm, int? idFarmGroup)
        {
            var (farm, farmError) = await EnsureOwnedFarmAsync(idFarm, forWrite: true);
            if (farmError != null)
            {
                return farmError;
            }
            if (idFarmGroup != null)
            {
                var (group, groupError) = await EnsureOwnedFarmGroupAsync(idFarmGroup.Value, forWrite: true);
                if (groupError != null)
                {
                    return groupError;
                }
                if (group!.TenantID != farm!.TenantID)
                {
                    return BadRequest("Farm and FarmGroup must belong to the same tenant.");
                }
            }
            await farmGroupRepo.FarmAssignToGroupAsync(idFarm, idFarmGroup);
            await WriteAuditAsync("FarmGroup.FarmAssigned", farm!.TenantID, "DeviceFarm", idFarm.ToString(), idFarmGroup?.ToString() ?? "(none)");
            return true;
        }

        /// Same shape as DeviceFarmUnitApiController.EnsureOwnedFarmAsync - duplicated per this codebase's "each controller owns its own EnsureOwned* helpers" convention rather than a shared cross-controller dependency.
        private Task<OwnedResult<DeviceFarm>> EnsureOwnedFarmAsync(int? idDeviceFarm, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmGetByIdAsync(idDeviceFarm), f => f.TenantID, "Farm", forWrite);

        private Task<OwnedResult<FarmGroup>> EnsureOwnedFarmGroupAsync(int idFarmGroup, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => farmGroupRepo.FarmGroupGetByIdAsync(idFarmGroup), g => g.TenantID, "FarmGroup", forWrite);
    }
}
