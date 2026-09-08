using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Global Admin-only content curation for the three horticulture subcatalogs - any tenant applies an entry's template from the Zone page (see DeviceFarmUnitController.ApplyHorticultureCatalog), but only a Global Admin edits the catalog itself, same "not a public/community catalog yet" scope as the roadmap's own explicit deferral.
    [Authorize(Roles = RoleNames.GlobalAdmin)]
    public class HorticultureCatalogController(IApi api) : Controller
    {
        public async Task<ActionResult> Index(HorticultureCatalogType type = HorticultureCatalogType.Crop)
        {
            ViewBag.CatalogType = type;
            return View(await api.HorticultureCatalogGet(type));
        }

        public ActionResult Create(HorticultureCatalogType type) =>
            View("Edit", new HorticultureCatalogEditViewModel { CatalogType = type });

        public async Task<ActionResult> Edit(HorticultureCatalogType type, int id) =>
            View(new HorticultureCatalogEditViewModel { CatalogType = type, Entry = await api.HorticultureCatalogGetById(type, id) });

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(HorticultureCatalogType type, int id)
        {
            await api.HorticultureCatalogDelete(type, id);
            return RedirectToAction(nameof(Index), new { type });
        }
    }
}
