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

        // Interim, pre-wizard form (restructure R defers the real sjetva wizard to R2) - SowingName is looked up/created as a Crop catalog row server-side, StartDate/ExpectedDurationDays get placeholder defaults the R2 wizard will let the user actually set.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropAdd(int idFarm, string farmOpenfieldCropName)
        {
            await api.CropAdd(new Sowing { FarmID = idFarm, SowingName = farmOpenfieldCropName });
            return RedirectToAction("Farms", "DeviceFarmUnit");
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropRename(int idSowing, string farmOpenfieldCropName)
        {
            Sowing crop = await api.CropGet(idSowing);
            crop.SowingName = farmOpenfieldCropName;
            await api.CropUpdate(crop);
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CropDelete(int idSowing)
        {
            await api.CropDelete(idSowing);
            return RedirectToAction("Farms", "DeviceFarmUnit");
        }

        // ---- Parcel list (crop detail) -----------------------------------

        public async Task<ActionResult> Parcels(int idSowing)
        {
            Sowing crop = await api.CropGet(idSowing);
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            DeviceFarm? farm = farms.FirstOrDefault(f => f.IDDeviceFarm == crop.FarmID);

            return View(new CropParcelsViewModel
            {
                Crop = crop,
                Farm = farm ?? new DeviceFarm(),
                Parcels = await api.ParcelDashboardListGet(idSowing),
            });
        }

        // Adding a parcel now creates a FarmParcel directly under the Farm (D2/D3), not under a Sowing - a Sowing's "parcels" are just whichever zones it currently occupies (dynamic, see Parcels() below). Real parcel-creation UI (with the farm picker, geometry, etc.) is a later restructure R session; this keeps the interim page compiling and working via the crop's own farm.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelAdd(int idSowing, string farmOpenfieldCropParcelName)
        {
            Sowing crop = await api.CropGet(idSowing);
            IList<FarmOpenfield> openfields = await api.FarmOpenfieldsGet();
            FarmOpenfield? openfield = openfields.FirstOrDefault(o => o.FarmID == crop.FarmID);
            if (openfield?.IDFarmOpenfield is int idFarmOpenfield)
            {
                await api.FarmParcelAdd(idFarmOpenfield, farmOpenfieldCropParcelName);
            }
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelDelete(int idFarmParcelZone, int idSowing)
        {
            await api.ParcelDelete(idFarmParcelZone);
            return RedirectToAction(nameof(Parcels), new { idSowing });
        }

        // ---- Parcel detail ------------------------------------------------

        public async Task<ActionResult> Parcel(int idFarmParcelZone)
        {
            ParcelViewModel model = await BuildParcelViewAsync(idFarmParcelZone);
            return View(model);
        }

        private async Task<ParcelViewModel> BuildParcelViewAsync(int idFarmParcelZone)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
            Sowing? crop = parcel.CurrentSowingID is int idSowing ? await api.CropGet(idSowing) : null;
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            DeviceFarm? farm = crop != null ? farms.FirstOrDefault(f => f.IDDeviceFarm == crop.FarmID) : null;

            IList<DeviceFleetStatus> devices = (await api.DeviceFleetGet())
                .Where(d => d.FarmParcelZoneID == idFarmParcelZone)
                .ToList();
            bool hasController = devices.Any(d => d.ControllerCapable);

            string? timeZone = User.GetTimeZone();
            return new ParcelViewModel
            {
                Parcel = parcel,
                Crop = crop,
                Farm = farm ?? new DeviceFarm(),
                Devices = devices,
                Rules = hasController ? await api.FarmParcelZoneRulesGet(idFarmParcelZone) : [],
                ManualOverrides = hasController ? await api.ParcelManualActuateStatus(idFarmParcelZone) : [],
                DisplayTimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
                DiscoveredDevices = await api.DiscoveryResultsGet(null, null, idFarmParcelZone),
                WifiConfigs = await api.DiscoveryWifiConfigsGet(),
            };
        }

        // ---- Manual Actuate - Open-Field's equivalent of DeviceFarmUnitController's ZoneManualActuateStart/Stop ----

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelManualActuateStart(int idFarmParcelZone, RelayFunction relayFunction, ManualOverrideMode mode,
            int? durationMinutes, SensorMetric? targetMetric, double? targetThreshold, double? targetHysteresis)
        {
            var request = new ManualActuateRequest(relayFunction, mode, durationMinutes is int m ? m * 60 : null, targetMetric, targetThreshold, targetHysteresis);
            try
            {
                await api.ParcelManualActuateStart(idFarmParcelZone, request);
                TempData["Message"] = $"{relayFunction} manually started.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelManualActuateStop(int idFarmParcelZone, RelayFunction relayFunction)
        {
            try
            {
                await api.ParcelManualActuateStop(idFarmParcelZone, relayFunction);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        // ---- Device discovery - Open-Field's equivalent of DeviceFarmUnitController's ScanZone/RegisterDiscoveredDeviceZone ----

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ScanParcel(DiscoveryScanRequest request)
        {
            try
            {
                await api.DiscoveryScan(request);
                TempData["Message"] = "Scan started - discovered devices will appear here shortly.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone = request.ParcelID });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterDiscoveredDeviceParcel(DiscoveryRegisterRequest request)
        {
            try
            {
                DiscoveryRegisterResult result = await api.DiscoveryRegister(request);
                var (message, error) = DiscoveryRegisterOutcomeMessage.For(result.Outcome);
                TempData["Message"] = message;
                TempData["Error"] = error;
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone = request.ParcelID });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelRename(int idFarmParcelZone, string farmOpenfieldCropParcelName)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
            parcel.FarmParcelZoneName = farmOpenfieldCropParcelName;
            await api.ParcelUpdate(parcel);
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        // All three null together means "no tank tracking", mirrors DeviceFarmUnitController.TankCalibrationUpdate exactly.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SafetyLimitsUpdate(int idFarmParcelZone, int? waterPumpMaxRunSeconds, int? waterPumpCooldownSeconds, bool skipWaterPumpWhenRainPredicted,
            int? heatingMaxRunSeconds, int? ventilationMaxRunSeconds, HeatingFailSafePolicyType? heatingFailSafePolicy,
            double? tankCapacityLiters, int? waterLevelRawEmpty, int? waterLevelRawFull, double? waterPumpMinLevel)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
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
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        // ---- Device assignment ---------------------------------------------

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> ParcelAssignPicker(int idFarmParcelZone, bool controllerCapable) =>
            View(new ParcelAssignPickerViewModel
            {
                IDFarmParcelZone = idFarmParcelZone,
                ControllerCapable = controllerCapable,
                Devices = await api.DeviceUnassignedGet(controllerCapable),
            });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Assign(int idDevice, int idFarmParcelZone, bool controllerCapable)
        {
            try
            {
                await api.ParcelDeviceAssign(new DeviceParcelAssignment { IDDevice = idDevice, IDFarmParcelZone = idFarmParcelZone });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Body);
                return View(nameof(ParcelAssignPicker), new ParcelAssignPickerViewModel
                {
                    IDFarmParcelZone = idFarmParcelZone,
                    ControllerCapable = controllerCapable,
                    Devices = await api.DeviceUnassignedGet(controllerCapable),
                });
            }
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Remove(int idDevice, int idFarmParcelZone)
        {
            await api.ParcelDeviceUnassign(idDevice);
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        // ---- Rules (Crop/Parcel scope) - Crop's own page, mirrors DeviceFarmUnitController.UnitRules; Parcel's rules are embedded inline in Parcel.cshtml instead, mirroring the Zone page. ----

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> SowingRules(int idSowing) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.Crop,
            ScopeId = idSowing,
            Rules = await api.SowingRulesGet(idSowing),
            RedirectActionName = nameof(SowingRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingRuleAdd(int idSowing, RuleFormInput input)
        {
            await AddRuleAsync(BuildOpenfieldRule(input, idSowing: idSowing), r => api.SowingRuleAdd(r));
            return RedirectToAction(nameof(SowingRules), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SowingRuleDelete(int idDeviceFarmUnitZoneRule, int idSowing)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.SowingRuleDelete(r));
            return RedirectToAction(nameof(SowingRules), new { idSowing });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmParcelZoneRuleAdd(int idFarmParcelZone, RuleFormInput input)
        {
            await AddRuleAsync(BuildOpenfieldRule(input, idFarmParcelZone: idFarmParcelZone), r => api.FarmParcelZoneRuleAdd(r));
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmParcelZoneRuleDelete(int idDeviceFarmUnitZoneRule, int idFarmParcelZone)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.FarmParcelZoneRuleDelete(r));
            return RedirectToAction(nameof(Parcel), new { idFarmParcelZone });
        }

        /// Mirrors DeviceFarmUnitController.BuildRule - exactly one of idSowing/idFarmParcelZone is non-null. RootConditionJson comes pre-built from wwwroot/js/rule-builder.js, same as BuildRule.
        private static DeviceFarmUnitZoneRule BuildOpenfieldRule(RuleFormInput input, int? idSowing = null, int? idFarmParcelZone = null)
        {
            ConditionNode? root = string.IsNullOrWhiteSpace(input.RootConditionJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<ConditionNode>(input.RootConditionJson, ConditionConfigJson.Options);
            return new DeviceFarmUnitZoneRule
            {
                DeviceSowingID = idSowing,
                DeviceFarmParcelZoneID = idFarmParcelZone,
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
