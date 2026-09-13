using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Global Admin-only content curation for the six crop subcatalogs - any organization applies an entry's template from the Zone page (see DeviceFarmUnitController.ApplyCropCatalog). An Organization admin can browse the catalog same as a Global reader (read-only, see Index/Edit.cshtml's isReadOnly), but still can't Save/Delete: it's one shared, cross-organization catalog, not per-organization data.
    [Authorize]
    public class CropCatalogController(IApi api) : Controller
    {
        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public async Task<ActionResult> Index(CropCatalogType type = CropCatalogType.Arable)
        {
            ViewBag.CatalogType = type;
            return View(await api.CropCatalogGet(type));
        }

        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public ActionResult Create(CropCatalogType type) =>
            View("Edit", new CropCatalogEditViewModel { CatalogType = type });

        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public async Task<ActionResult> Details(CropCatalogType type, int id) =>
            View(new CropCatalogEditViewModel { CatalogType = type, Entry = await api.CropCatalogGetById(type, id) });

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Save(CropCatalogEditViewModel value)
        {
            if (!ModelState.IsValid)
            {
                return View("Edit", value);
            }
            await api.CropCatalogAdd(value.CatalogType, value.Entry);
            return RedirectToAction(nameof(Index), new { type = value.CatalogType });
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(CropCatalogType type, int id)
        {
            await api.CropCatalogDelete(type, id);
            return RedirectToAction(nameof(Index), new { type });
        }
    }
}
