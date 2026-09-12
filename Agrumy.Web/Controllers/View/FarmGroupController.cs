using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Farm Groups - a cross-cutting overview ABOVE Farm, spanning FarmType (Greenhouse/Crop/Fruit); purely additive, does not replace or restructure the existing Greenhouse/Open-Field farming pages.
    [Authorize]
    public class FarmGroupController(IApi api) : Controller
    {
        public async Task<ActionResult> Index()
        {
            IList<FarmGroup> groups = await api.FarmGroupsGet();
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            var summaries = groups.Select(g => new FarmGroupSummaryViewModel
            {
                Group = g,
                FarmCount = farms.Count(f => f.FarmGroupID == g.IDFarmGroup),
            }).ToList();
            return View(new FarmGroupIndexViewModel { Groups = summaries });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string name)
        {
            FarmGroup added = await api.FarmGroupCreate(name);
            return RedirectToAction(nameof(Details), new { id = added.IDFarmGroup });
        }

        public async Task<ActionResult> Details(int id)
        {
            IList<FarmGroup> groups = await api.FarmGroupsGet();
            FarmGroup? group = groups.FirstOrDefault(g => g.IDFarmGroup == id);
            if (group == null)
            {
                return NotFound();
            }
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            return View(new FarmGroupDetailsViewModel
            {
                Group = group,
                GreenhouseFarms = farms.Where(f => f.FarmGroupID == id && f.FarmType == FarmType.Greenhouse).ToList(),
                CropFarms = farms.Where(f => f.FarmGroupID == id && f.FarmType == FarmType.OpenField).ToList(),
                UngroupedGreenhouseFarms = farms.Where(f => f.FarmGroupID == null && f.FarmType == FarmType.Greenhouse).ToList(),
                UngroupedCropFarms = farms.Where(f => f.FarmGroupID == null && f.FarmType == FarmType.OpenField).ToList(),
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddFarm(int id, int idFarm)
        {
            await api.FarmAssignToGroup(idFarm, id);
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveFarm(int id, int idFarm)
        {
            await api.FarmAssignToGroup(idFarm, null);
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int id)
        {
            await api.FarmGroupDelete(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
