using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Global Admin-only content curation for the three horticulture subcatalogs - any organization applies an entry's template from the Zone page (see DeviceFarmUnitController.ApplyHorticultureCatalog). An Organization admin can browse the catalog same as a Global reader (read-only, see Index/Edit.cshtml's isReadOnly), but still can't Save/Delete: it's one shared, cross-organization catalog, not per-organization data.
    [Authorize]
    public class HorticultureCatalogController(IApi api) : Controller
    {
        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public async Task<ActionResult> Index(HorticultureCatalogType type = HorticultureCatalogType.Crop)
        {
            ViewBag.CatalogType = type;
            return View(await api.HorticultureCatalogGet(type));
        }

        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public ActionResult Create(HorticultureCatalogType type) =>
            View("Edit", new HorticultureCatalogEditViewModel { CatalogType = type });

        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        public async Task<ActionResult> Edit(HorticultureCatalogType type, int id) =>
            View(new HorticultureCatalogEditViewModel { CatalogType = type, Entry = await api.HorticultureCatalogGetById(type, id) });

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Save(HorticultureCatalogEditViewModel value)
        {
            if (!ModelState.IsValid)
            {
                return View("Edit", value);
            }
            if (value.Entry.ID is int)
            {
                await api.HorticultureCatalogUpdate(value.CatalogType, value.Entry);
            }
            else
            {
                await api.HorticultureCatalogAdd(value.CatalogType, value.Entry);
            }
            return RedirectToAction(nameof(Index), new { type = value.CatalogType });
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(HorticultureCatalogType type, int id)
        {
            await api.HorticultureCatalogDelete(type, id);
            return RedirectToAction(nameof(Index), new { type });
        }
    }
}
