using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// A Global admin/reader edits the server-wide alert defaults (ServerConfig's Alerts section); a Tenant admin edits their own tenant's TenantAlertConfig override instead - same nav item and URL, the Index view picks by role.
    [Authorize]
    public class AlertsController(IApi api) : Controller
    {
        private bool IsGlobal => User.IsInRole(RoleNames.GlobalAdmin) || User.IsInRole(RoleNames.GlobalReader);

        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public async Task<ActionResult> Index() =>
            IsGlobal ? View(await api.ServerConfigAlertsGet()) : View("TenantIndex", await api.TenantAlertConfigGet());

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(AlertSettings settings)
        {
            if (!ModelState.IsValid)
            {
                return View(settings);
            }

            try
            {
                await api.ServerConfigAlertsUpdate(settings);
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(ApiErrorField.Resolve<AlertSettings>(ex.Body), ex.Body);
                return View(settings);
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
