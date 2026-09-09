using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    [Authorize]
    public class WifiConfigController(IApi api) : Controller
    {
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> Index() => View(await api.DiscoveryWifiConfigsGet());

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Add(TenantWifiConfig config)
        {
            try
            {
                await api.DiscoveryWifiConfigAdd(config);
                TempData["Message"] = "WiFi network saved.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Update(int idTenantWifiConfig, TenantWifiConfig config)
        {
            try
            {
                await api.DiscoveryWifiConfigUpdate(idTenantWifiConfig, config);
                TempData["Message"] = "WiFi network updated.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int idTenantWifiConfig)
        {
            try
            {
                await api.DiscoveryWifiConfigDelete(idTenantWifiConfig);
                TempData["Message"] = "WiFi network removed.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
