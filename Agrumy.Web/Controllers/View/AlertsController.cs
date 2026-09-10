using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Alerts settings, split out of Server Settings into their own sidebar page (roadmap #338).
    /// A Global admin/reader edits the server-wide DEFAULTS (still the same ServerConfig model/endpoint
    /// as before). A Tenant admin instead edits their OWN tenant's override of those same thresholds
    /// (roadmap #509, TenantAlertConfig) - the Index view renders one or the other depending on role,
    /// so both share the "Alerts" nav item and URL.
    [Authorize]
    public class AlertsController(IApi api) : Controller
    {
        private bool IsGlobal => User.IsInRole(RoleNames.GlobalAdmin) || User.IsInRole(RoleNames.GlobalReader);

        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public async Task<ActionResult> Index() =>
            IsGlobal ? View(await api.ServerConfigGet()) : View("TenantIndex", await api.TenantAlertConfigGet());

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(ServerConfig serverConfig)
        {
            if (!ModelState.IsValid)
            {
                return View(serverConfig);
            }

            try
            {
                await api.ServerConfigUpdate(serverConfig);
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(nameof(ServerConfig.ProblemEventExpiryHours), ex.Body);
                return View(serverConfig);
            }

            TempData["Message"] = "Alert settings saved.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = RoleNames.TenantAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TenantIndex(TenantAlertConfig config)
        {
            try
            {
                await api.TenantAlertConfigUpdate(config);
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(nameof(TenantAlertConfig.ProblemEventExpiryHours), ex.Body);
                return View(config);
            }

            TempData["Message"] = "Alert settings saved.";
            return RedirectToAction(nameof(Index));
        }
    }
}
