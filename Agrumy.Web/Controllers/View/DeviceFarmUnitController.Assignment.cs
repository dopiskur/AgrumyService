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
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> AssignPicker(int idDeviceFarmUnitZone, bool controllerCapable) =>
            View(new AssignPickerViewModel
            {
                IDDeviceFarmUnitZone = idDeviceFarmUnitZone,
                ControllerCapable = controllerCapable,
                Devices = await api.DeviceUnassignedGet(controllerCapable),
            });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Assign(int idDevice, int idDeviceFarmUnitZone, bool controllerCapable)
        {
            try
            {
                await api.DeviceAssign(new DeviceZoneAssignment { IDDevice = idDevice, IDDeviceFarmUnitZone = idDeviceFarmUnitZone });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Body);
                return View(nameof(AssignPicker), new AssignPickerViewModel
                {
                    IDDeviceFarmUnitZone = idDeviceFarmUnitZone,
                    ControllerCapable = controllerCapable,
                    Devices = await api.DeviceUnassignedGet(controllerCapable),
                });
            }

            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Remove(int idDevice, int idDeviceFarmUnitZone)
        {
            await api.DeviceUnassign(idDevice);
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        // returnUrl comes from window.location client-side (_ZoneStatusBadge) since Request.Path server-side would be the AJAX poll endpoint, not the visible page; falls back to Farms if missing/unsafe.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AcknowledgeAlert(int idEventDevice, string? returnUrl)
        {
            await api.DeviceEventAcknowledge(idEventDevice);
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Farms));
        }
    }
}
