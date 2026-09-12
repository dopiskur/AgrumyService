using Agrumy.Web.Security;
using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Shared.Utils;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    public partial class DeviceFarmUnitController
    {
        // ---- Recycle Bin (roadmap #409/#427) --------------------------------

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> RecycleBin()
        {
            ServerConfig config = await api.ServerConfigGet();
            return View(new RecycleBinViewModel
            {
                Devices = await api.RecycleBinDevicesGet(),
                Farms = await api.RecycleBinFarmsGet(),
                PendingPurgeDevices = await api.RecycleBinDevicesPendingPurgeGet(),
                PendingPurgeFarms = await api.RecycleBinFarmsPendingPurgeGet(),
                RecycleBinRetentionDays = config.RecycleBinRetentionDays ?? 30,
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RecycleBinRestoreDevice(int idDevice)
        {
            await api.RecycleBinDeviceRestore(idDevice);
            return RedirectToAction(nameof(RecycleBin));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RecycleBinRestoreFarm(int idDeviceFarm)
        {
            await api.RecycleBinFarmRestore(idDeviceFarm);
            return RedirectToAction(nameof(RecycleBin));
        }

        // Global Admin-only, confirmation-phrase-gated (roadmap #427) - JSON round trip like ServerConfigController's DataMaintenancePurge*, not a redirecting form POST, since the JS needs the result to update the page without a full reload.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RecycleBinPurgeDevice(int idDevice, [FromBody] RecycleBinPurgeRequest request)
        {
            try
            {
                await api.RecycleBinDevicePurgeNow(idDevice, request);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RecycleBinPurgeFarm(int idDeviceFarm, [FromBody] RecycleBinPurgeRequest request)
        {
            try
            {
                await api.RecycleBinFarmPurgeNow(idDeviceFarm, request);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RecycleBinEmpty([FromBody] RecycleBinPurgeRequest request)
        {
            try
            {
                RecycleBinEmptyResult result = await api.RecycleBinEmpty(request);
                return Ok(result);
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }
    }
}
