using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// One-click, tenant-wide fail-closed actuator stop - reachable from every page via _Layout.cshtml, not tied to the Global-admin-only Tenant Management area.
    [Authorize(Roles = RoleNames.DeviceManagers)]
    public class EmergencyStopController(IApi api) : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Activate(string? returnUrl)
        {
            try
            {
                await api.EmergencyStopActivate();
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Clear(string? returnUrl)
        {
            try
            {
                await api.EmergencyStopClear();
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
        }
    }
}
