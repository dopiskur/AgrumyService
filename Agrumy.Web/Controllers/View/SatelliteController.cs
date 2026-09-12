using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Tenant satellite module settings (Detaljni dizajn S, D1/D8; collection choice is S-B2) - a Tenant admin always edits their own tenant (idTenant omitted, the API resolves it from the caller), a Global admin/reader can view/edit any tenant by passing idTenant explicitly.
    [Authorize]
    public class SatelliteController(IApi api) : Controller
    {
        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public async Task<ActionResult> Index(int? idTenant)
        {
            TenantSatelliteConfig config = await api.TenantSatelliteConfigGet(idTenant);
            ViewBag.IdTenant = idTenant;
            return View(config);
        }

        [Authorize(Roles = RoleNames.Admins)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(TenantSatelliteConfig config, int? idTenant, List<SatelliteIndex>? defaultIndices)
        {
            config.DefaultIndices = defaultIndices ?? [];
            try
            {
                await api.TenantSatelliteConfigUpdate(config, idTenant);
                TempData["Message"] = "Satellite settings saved.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index), new { idTenant });
        }

        [Authorize(Roles = RoleNames.Admins)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Test(string? clientId, string? clientSecret, int? idTenant)
        {
            SatelliteConfigTestResult result = await api.TenantSatelliteConfigTest(new SatelliteConfigTestRequest { ClientId = clientId, ClientSecret = clientSecret }, idTenant);
            TempData[result.Ok ? "Message" : "Error"] = result.Ok ? "Connection test succeeded - credentials saved." : result.Error;
            return RedirectToAction(nameof(Index), new { idTenant });
        }
    }
}
