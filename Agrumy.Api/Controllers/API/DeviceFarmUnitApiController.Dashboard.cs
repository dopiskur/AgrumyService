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
    public partial class DeviceFarmUnitApiController
    {
        /// Top-level Unit cubes - read-only, open to any authenticated caller (same reasoning as DeviceApiController.DeviceFleetGet).
        [Authorize]
        [HttpGet("Dashboard")]
        public async Task<ActionResult<IList<DeviceFarmUnitDashboard>>> DeviceFarmUnitDashboardGet() =>
            Ok(await deviceFarmUnitRepo.DeviceFarmUnitDashboardGetAsync(CallerReadsDevicesGlobally ? null : CallerTenantId));

        [Authorize]
        [HttpGet("Dashboard/Zones")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneDashboard>>> DeviceFarmUnitZoneDashboardListGet(int? idDeviceFarmUnit)
        {
            var (unit, error) = await EnsureOwnedUnitAsync(idDeviceFarmUnit, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.DeviceFarmUnitZoneDashboardListGetAsync(unit!.IDDeviceFarmUnit!.Value));
        }

        [Authorize]
        [HttpGet("Dashboard/Zone")]
        public async Task<ActionResult<DeviceFarmUnitZoneDashboard>> DeviceFarmUnitZoneDashboardGet(int? idDeviceFarmUnitZone)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            DeviceFarmUnitZoneDashboard? dashboard = await deviceFarmUnitRepo.DeviceFarmUnitZoneDashboardForDisplayGetAsync(zone!.IDDeviceFarmUnitZone!.Value);
            return dashboard is null ? NotFound() : Ok(dashboard);
        }

        /// One dashboard widget's own (level, levelId) scope - a widget on any zone's page can read a different Farm/Unit/Zone than the page itself, so ownership is checked against the widget's OWN target, not the page's.
        [Authorize]
        [HttpGet("Dashboard/Widget")]
        public async Task<ActionResult<DashboardAggregate>> DashboardWidgetAggregateGet(HierarchyNodeKind level, int levelId)
        {
            ActionResult? error = await EnsureOwnedAggregationTargetAsync(level, levelId, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.DashboardAggregateGetAsync(level, levelId));
        }


        /// Same shape as DeviceApiController.EnsureOwnedDeviceAsync, for DeviceFarm (roadmap #384).
        private Task<OwnedResult<DeviceFarm>> EnsureOwnedFarmAsync(int? idDeviceFarm, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmGetByIdAsync(idDeviceFarm), f => f.TenantID, "Farm", forWrite);

        /// Same shape as DeviceApiController.EnsureOwnedDeviceAsync, for DeviceFarmUnit - see ApiControllerBase.EnsureOwnedDeviceEntityAsync for the shared 404/403 logic.
        private Task<OwnedResult<DeviceFarmUnit>> EnsureOwnedUnitAsync(int? idDeviceFarmUnit, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => deviceFarmUnitRepo.DeviceFarmUnitGetByIdAsync(idDeviceFarmUnit), u => u.TenantID, "Unit", forWrite);
    }
}
