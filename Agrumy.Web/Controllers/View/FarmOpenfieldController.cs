using Agrumy.Web.Security;
using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Crop/Parcel CRUD, device assignment, and safety-limit editing - the Open-Field mirror of DeviceFarmUnitController's Zone/Unit pages. Farm-level actions (Add/Rename/Delete/rule pages) stay on DeviceFarmUnitController, shared by both branches - this controller only owns what's genuinely new.
    [Authorize]
    public class FarmOpenfieldController(IApi api) : Controller
    {
        // ---- Crop CRUD --------------------------------------------------

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropAdd(int idFarmOpenfield, string farmOpenfieldCropName)
        {
            await api.CropAdd(new FarmOpenfieldCrop { FarmOpenfieldID = idFarmOpenfield, FarmOpenfieldCropName = farmOpenfieldCropName });
            return RedirectToAction("Farms", "DeviceFarmUnit");
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropRename(int idFarmOpenfieldCrop, string farmOpenfieldCropName)
        {
            FarmOpenfieldCrop crop = await api.CropGet(idFarmOpenfieldCrop);
            crop.FarmOpenfieldCropName = farmOpenfieldCropName;
            await api.CropUpdate(crop);
            return RedirectToAction(nameof(Parcels), new { idFarmOpenfieldCrop });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropDelete(int idFarmOpenfieldCrop)
        {
            await api.CropDelete(idFarmOpenfieldCrop);
            return RedirectToAction("Farms", "DeviceFarmUnit");
        }

        // ---- Parcel list (crop detail) -----------------------------------

        public async Task<ActionResult> Parcels(int idFarmOpenfieldCrop)
        {
            FarmOpenfieldCrop crop = await api.CropGet(idFarmOpenfieldCrop);
            IList<FarmOpenfield> openfields = await api.FarmOpenfieldsGet();
            FarmOpenfield? openfield = openfields.FirstOrDefault(o => o.IDFarmOpenfield == crop.FarmOpenfieldID);
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            DeviceFarm? farm = openfield is null ? null : farms.FirstOrDefault(f => f.IDDeviceFarm == openfield.FarmID);

            return View(new CropParcelsViewModel
            {
                Crop = crop,
                Farm = farm ?? new DeviceFarm(),
                Parcels = await api.ParcelDashboardListGet(idFarmOpenfieldCrop),
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelAdd(int idFarmOpenfieldCrop, string farmOpenfieldCropParcelName)
        {
            await api.ParcelAdd(new FarmOpenfieldCropParcel { FarmOpenfieldCropID = idFarmOpenfieldCrop, FarmOpenfieldCropParcelName = farmOpenfieldCropParcelName });
            return RedirectToAction(nameof(Parcels), new { idFarmOpenfieldCrop });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelDelete(int idFarmOpenfieldCropParcel, int idFarmOpenfieldCrop)
        {
            await api.ParcelDelete(idFarmOpenfieldCropParcel);
            return RedirectToAction(nameof(Parcels), new { idFarmOpenfieldCrop });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelMigrate(int idFarmOpenfieldCropParcel, int idTargetFarmOpenfieldCrop, int idFarmOpenfieldCrop)
        {
            try
            {
                await api.ParcelMigrate(idFarmOpenfieldCropParcel, idTargetFarmOpenfieldCrop);
                TempData["Message"] = "Parcel migrated.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcels), new { idFarmOpenfieldCrop });
        }

        // ---- Parcel detail ------------------------------------------------

        public async Task<ActionResult> Parcel(int idFarmOpenfieldCropParcel)
        {
            ParcelViewModel model = await BuildParcelViewAsync(idFarmOpenfieldCropParcel);
            return View(model);
        }

        private async Task<ParcelViewModel> BuildParcelViewAsync(int idFarmOpenfieldCropParcel)
        {
            FarmOpenfieldCropParcel parcel = await api.ParcelGetById(idFarmOpenfieldCropParcel);
            FarmOpenfieldCrop crop = await api.CropGet(parcel.FarmOpenfieldCropID);
            IList<FarmOpenfield> openfields = await api.FarmOpenfieldsGet();
            FarmOpenfield? openfield = openfields.FirstOrDefault(o => o.IDFarmOpenfield == crop.FarmOpenfieldID);
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            DeviceFarm? farm = openfield is null ? null : farms.FirstOrDefault(f => f.IDDeviceFarm == openfield.FarmID);

            IList<DeviceFleetStatus> devices = (await api.DeviceFleetGet())
                .Where(d => d.FarmOpenfieldCropParcelID == idFarmOpenfieldCropParcel)
                .ToList();
            bool hasController = devices.Any(d => d.ControllerCapable);

            return new ParcelViewModel
            {
                Parcel = parcel,
                Crop = crop,
                Farm = farm ?? new DeviceFarm(),
                Devices = devices,
                Rules = hasController ? await api.FarmOpenfieldCropParcelRulesGet(idFarmOpenfieldCropParcel) : [],
            };
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelRename(int idFarmOpenfieldCropParcel, string farmOpenfieldCropParcelName)
        {
            FarmOpenfieldCropParcel parcel = await api.ParcelGetById(idFarmOpenfieldCropParcel);
            parcel.FarmOpenfieldCropParcelName = farmOpenfieldCropParcelName;
            await api.ParcelUpdate(parcel);
            return RedirectToAction(nameof(Parcel), new { idFarmOpenfieldCropParcel });
        }

        // All three null together means "no tank tracking", mirrors DeviceFarmUnitController.TankCalibrationUpdate exactly.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SafetyLimitsUpdate(int idFarmOpenfieldCropParcel, int? waterPumpMaxRunSeconds, int? waterPumpCooldownSeconds, bool skipWaterPumpWhenRainPredicted,
            int? heatingMaxRunSeconds, int? ventilationMaxRunSeconds, HeatingFailSafePolicyType? heatingFailSafePolicy,
            double? tankCapacityLiters, int? waterLevelRawEmpty, int? waterLevelRawFull, double? waterPumpMinLevel)
        {
            FarmOpenfieldCropParcel parcel = await api.ParcelGetById(idFarmOpenfieldCropParcel);
            parcel.WaterPumpMaxRunSeconds = waterPumpMaxRunSeconds;
            parcel.WaterPumpCooldownSeconds = waterPumpCooldownSeconds;
            parcel.SkipWaterPumpWhenRainPredicted = skipWaterPumpWhenRainPredicted;
            parcel.HeatingMaxRunSeconds = heatingMaxRunSeconds;
            parcel.VentilationMaxRunSeconds = ventilationMaxRunSeconds;
            parcel.HeatingFailSafePolicy = heatingFailSafePolicy;
            parcel.TankCapacityLiters = tankCapacityLiters;
            parcel.WaterLevelRawEmpty = waterLevelRawEmpty;
            parcel.WaterLevelRawFull = waterLevelRawFull;
            parcel.WaterPumpMinLevel = waterPumpMinLevel;
            try
            {
                await api.ParcelUpdate(parcel);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmOpenfieldCropParcel });
        }

        // ---- Device assignment ---------------------------------------------

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> ParcelAssignPicker(int idFarmOpenfieldCropParcel, bool controllerCapable) =>
            View(new ParcelAssignPickerViewModel
            {
                IDFarmOpenfieldCropParcel = idFarmOpenfieldCropParcel,
                ControllerCapable = controllerCapable,
                Devices = await api.DeviceUnassignedGet(controllerCapable),
            });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Assign(int idDevice, int idFarmOpenfieldCropParcel, bool controllerCapable)
        {
            try
            {
                await api.ParcelDeviceAssign(new DeviceParcelAssignment { IDDevice = idDevice, IDFarmOpenfieldCropParcel = idFarmOpenfieldCropParcel });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Body);
                return View(nameof(ParcelAssignPicker), new ParcelAssignPickerViewModel
                {
                    IDFarmOpenfieldCropParcel = idFarmOpenfieldCropParcel,
                    ControllerCapable = controllerCapable,
                    Devices = await api.DeviceUnassignedGet(controllerCapable),
                });
            }
            return RedirectToAction(nameof(Parcel), new { idFarmOpenfieldCropParcel });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Remove(int idDevice, int idFarmOpenfieldCropParcel)
        {
            await api.ParcelDeviceUnassign(idDevice);
            return RedirectToAction(nameof(Parcel), new { idFarmOpenfieldCropParcel });
        }

        // ---- Rules (Crop/Parcel scope) - Crop's own page, mirrors DeviceFarmUnitController.UnitRules; Parcel's rules are embedded inline in Parcel.cshtml instead, mirroring the Zone page. ----

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> FarmOpenfieldCropRules(int idFarmOpenfieldCrop) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.Crop,
            ScopeId = idFarmOpenfieldCrop,
            Rules = await api.FarmOpenfieldCropRulesGet(idFarmOpenfieldCrop),
            RedirectActionName = nameof(FarmOpenfieldCropRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmOpenfieldCropRuleAdd(int idFarmOpenfieldCrop, RuleFormInput input)
        {
            await AddRuleAsync(BuildOpenfieldRule(input, idFarmOpenfieldCrop: idFarmOpenfieldCrop), r => api.FarmOpenfieldCropRuleAdd(r));
            return RedirectToAction(nameof(FarmOpenfieldCropRules), new { idFarmOpenfieldCrop });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmOpenfieldCropRuleDelete(int idDeviceFarmUnitZoneRule, int idFarmOpenfieldCrop)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.FarmOpenfieldCropRuleDelete(r));
            return RedirectToAction(nameof(FarmOpenfieldCropRules), new { idFarmOpenfieldCrop });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmOpenfieldCropParcelRuleAdd(int idFarmOpenfieldCropParcel, RuleFormInput input)
        {
            await AddRuleAsync(BuildOpenfieldRule(input, idFarmOpenfieldCropParcel: idFarmOpenfieldCropParcel), r => api.FarmOpenfieldCropParcelRuleAdd(r));
            return RedirectToAction(nameof(Parcel), new { idFarmOpenfieldCropParcel });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmOpenfieldCropParcelRuleDelete(int idDeviceFarmUnitZoneRule, int idFarmOpenfieldCropParcel)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.FarmOpenfieldCropParcelRuleDelete(r));
            return RedirectToAction(nameof(Parcel), new { idFarmOpenfieldCropParcel });
        }

        /// Mirrors DeviceFarmUnitController.BuildRule - exactly one of idFarmOpenfieldCrop/idFarmOpenfieldCropParcel is non-null. RootConditionJson comes pre-built from wwwroot/js/rule-builder.js, same as BuildRule.
        private static DeviceFarmUnitZoneRule BuildOpenfieldRule(RuleFormInput input, int? idFarmOpenfieldCrop = null, int? idFarmOpenfieldCropParcel = null)
        {
            ConditionNode? root = string.IsNullOrWhiteSpace(input.RootConditionJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<ConditionNode>(input.RootConditionJson, ConditionConfigJson.Options);
            return new DeviceFarmUnitZoneRule
            {
                DeviceFarmOpenfieldCropID = idFarmOpenfieldCrop,
                DeviceFarmOpenfieldCropParcelID = idFarmOpenfieldCropParcel,
                ActionType = input.ActionType,
                RelayFunction = input.ActionType == ActionType.Relay ? input.RelayFunction : null,
                Name = input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                TargetPercent = input.ActionType == ActionType.Relay ? input.TargetPercent : null,
                IsSafetyRule = input.IsSafetyRule,
                NotificationSubject = input.ActionType == ActionType.Notification ? input.NotificationSubject : null,
                NotificationBody = input.ActionType == ActionType.Notification ? input.NotificationBody : null,
                Root = root,
            };
        }

        private async Task AddRuleAsync(DeviceFarmUnitZoneRule rule, Func<DeviceFarmUnitZoneRule, Task<RuleAddResult>> add)
        {
            try
            {
                RuleAddResult result = await add(rule);
                if (result.ScopeConflictWarning != null)
                {
                    TempData["Warning"] = result.ScopeConflictWarning;
                }
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
        }

        private async Task DeleteRuleAsync(int idRule, Func<int?, Task> delete)
        {
            try
            {
                await delete(idRule);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
        }
    }
}
