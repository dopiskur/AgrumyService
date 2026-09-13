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
            var cropFarms = farms.Where(f => f.FarmGroupID == id && f.FarmType == FarmType.OpenField).ToList();
            var greenhouseFarms = farms.Where(f => f.FarmGroupID == id && f.FarmType == FarmType.Greenhouse).ToList();

            var cropParcelsWithArea = 0;
            var cropParcelsTotal = 0;
            var cropAreaHa = 0.0;
            foreach (DeviceFarm farm in cropFarms)
            {
                foreach (FarmParcel parcel in await api.FarmParcelsGet(farm.IDDeviceFarm!.Value))
                {
                    cropParcelsTotal++;
                    if (parcel.AreaHectares is double ha)
                    {
                        cropParcelsWithArea++;
                        cropAreaHa += ha;
                    }
                }
            }

            var greenhouseFarmIds = greenhouseFarms.Select(f => f.IDDeviceFarm).ToHashSet();
            var groupUnits = (await api.DeviceFarmUnitsGet()).Where(u => greenhouseFarmIds.Contains(u.DeviceFarmID)).ToList();
            int greenhouseUnitsWithArea = groupUnits.Count(u => u.AreaHectares != null);
            double greenhouseAreaHa = groupUnits.Where(u => u.AreaHectares != null).Sum(u => u.AreaHectares!.Value);

            return View(new FarmGroupDetailsViewModel
            {
                Group = group,
                GreenhouseFarms = greenhouseFarms,
                CropFarms = cropFarms,
                UngroupedGreenhouseFarms = farms.Where(f => f.FarmGroupID == null && f.FarmType == FarmType.Greenhouse).ToList(),
                UngroupedCropFarms = farms.Where(f => f.FarmGroupID == null && f.FarmType == FarmType.OpenField).ToList(),
                CropAreaSummary = new ParcelAreaSummaryViewModel { TotalHectares = cropAreaHa, WithAreaCount = cropParcelsWithArea, TotalCount = cropParcelsTotal },
                GreenhouseAreaSummary = new ParcelAreaSummaryViewModel { TotalHectares = greenhouseAreaHa, WithAreaCount = greenhouseUnitsWithArea, TotalCount = groupUnits.Count },
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

        /// The only remaining way to create a new Open-Field farm - the old standalone "Farms" register and its Crop-page parking spot are both gone, Farm Groups is the sole entry point now.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateCropFarm(int id, string farmName)
        {
            DeviceFarm farm = await api.FarmOpenfieldCreate(farmName);
            await api.FarmAssignToGroup(farm.IDDeviceFarm!.Value, id);
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateGreenhouseFarm(int id, string farmName)
        {
            DeviceFarm farm = await api.DeviceFarmAdd(new DeviceFarm { DeviceFarmName = farmName, FarmType = FarmType.Greenhouse });
            await api.FarmAssignToGroup(farm.IDDeviceFarm!.Value, id);
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
