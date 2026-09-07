using Agrumy.Web.Security;
using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Shared.Utils;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    [Authorize]
    public class DeviceFarmUnitController(IApi api) : Controller
    {
        public async Task<ActionResult> Index() => View(await BuildGroupedUnitCubesAsync());

        // Shared by the full page and its 10s-polled fragment (IndexCubes below) so a live update never reverts the farm grouping.
        private async Task<GroupedUnitCubesViewModel> BuildGroupedUnitCubesAsync() => new()
        {
            Units = await api.DeviceFarmUnitDashboardGet(),
            Farms = await api.DeviceFarmsGet(),
        };

        // ---- Farm (roadmap #384) --------------------------------------

        public async Task<ActionResult> Farms() => View(new FarmListViewModel
        {
            Farms = await api.DeviceFarmsGet(),
            Units = await api.DeviceFarmUnitsGet(),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmAdd(string deviceFarmName)
        {
            await api.DeviceFarmAdd(new DeviceFarm { DeviceFarmName = deviceFarmName });
            return RedirectToAction(nameof(Farms));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmRename(int idDeviceFarm, string deviceFarmName)
        {
            await api.DeviceFarmUpdate(new DeviceFarm { IDDeviceFarm = idDeviceFarm, DeviceFarmName = deviceFarmName });
            return RedirectToAction(nameof(Farms));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmDelete(int idDeviceFarm)
        {
            await api.DeviceFarmDelete(idDeviceFarm);
            return RedirectToAction(nameof(Farms));
        }

        /// Whole-object PUT semantics (same as UnitRename) - fetches the unit first so DeviceFarmUnitName isn't wiped by a partial payload. idDeviceFarm null unassigns.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitAssignFarm(int idDeviceFarmUnit, int? idDeviceFarm)
        {
            DeviceFarmUnit unit = await api.DeviceFarmUnitGet(idDeviceFarmUnit);
            unit.DeviceFarmID = idDeviceFarm;
            await api.DeviceFarmUnitUpdate(unit);
            return RedirectToAction(nameof(Farms));
        }

        /// Roadmap #408 (b) - the "migrate first" offer in FarmDelete's confirmation flow: every unit still on idDeviceFarm moves to idTargetFarm, one at a time (same UnitAssignFarm write, just looped) - called before FarmDelete, never together with it in one request.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmMigrateUnits(int idDeviceFarm, int idTargetFarm)
        {
            var units = (await api.DeviceFarmUnitsGet()).Where(u => u.DeviceFarmID == idDeviceFarm).ToList();
            foreach (DeviceFarmUnit unit in units)
            {
                unit.DeviceFarmID = idTargetFarm;
                await api.DeviceFarmUnitUpdate(unit);
            }
            return Ok();
        }

        // ---- Recycle Bin (roadmap #409) --------------------------------

        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult> RecycleBin()
        {
            ServerConfig config = await api.ServerConfigGet();
            return View(new RecycleBinViewModel
            {
                Devices = await api.RecycleBinDevicesGet(),
                Farms = await api.RecycleBinFarmsGet(),
                RecycleBinRetentionDays = config.RecycleBinRetentionDays ?? 30,
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RecycleBinRestoreDevice(int idDevice)
        {
            await api.RecycleBinDeviceRestore(idDevice);
            return RedirectToAction(nameof(RecycleBin));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RecycleBinRestoreFarm(int idDeviceFarm)
        {
            await api.RecycleBinFarmRestore(idDeviceFarm);
            return RedirectToAction(nameof(RecycleBin));
        }

        public async Task<ActionResult> IndexCubes() => PartialView("_UnitCubesGrouped", await BuildGroupedUnitCubesAsync());

        public async Task<ActionResult> Zones(int idDeviceFarmUnit)
        {
            IList<DeviceFarmUnitZoneDashboard> zones = await api.DeviceFarmUnitZoneDashboardListGet(idDeviceFarmUnit);
            if (zones.Count == 1)
            {
                return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone = zones[0].IDDeviceFarmUnitZone });
            }

            string? timeZone = User.GetTimeZone();
            return View(new UnitZonesViewModel
            {
                Unit = await api.DeviceFarmUnitGet(idDeviceFarmUnit),
                Farms = await api.DeviceFarmsGet(),
                Zones = zones,
                DisplayTimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
                // Last 24h, hourly buckets - same window _ZoneDetails' sparkline trend already uses.
                SensorDataJson = await api.SensorDataUnitAverageGet(idDeviceFarmUnit, 24, 1),
                DiscoveredDevices = await api.DiscoveryResultsGet(idDeviceFarmUnit, null),
                WifiConfigs = await api.DiscoveryWifiConfigsGet(),
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ScanUnit(DiscoveryScanRequest request)
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
            return RedirectToAction(nameof(Zones), new { idDeviceFarmUnit = request.UnitID });
        }

        /// Roadmap #411 - bulk WiFi switch for every device under the unit, reusing #355's per-device mechanism (see DeviceFarmUnitApiController.UnitWifiUpdate).
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitWifiUpdate(int idDeviceFarmUnit, string ssid, string wifiPassword)
        {
            try
            {
                UnitWifiUpdateResult result = await api.UnitWifiUpdate(idDeviceFarmUnit, new UnitWifiUpdateRequest { Ssid = ssid, WifiPassword = wifiPassword });
                int skipped = result.DeviceCount - result.IssuedCount;
                TempData["Message"] = skipped == 0
                    ? $"WiFi switch requested for all {result.DeviceCount} device(s) in this unit."
                    : $"WiFi switch requested for {result.IssuedCount} of {result.DeviceCount} device(s) - {skipped} already had one pending, skipped.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zones), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterDiscoveredDeviceFarmUnit(DiscoveryRegisterRequest request)
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
            return RedirectToAction(nameof(Zones), new { idDeviceFarmUnit = request.UnitID });
        }

        public async Task<ActionResult> ZonesCubes(int idDeviceFarmUnit) =>
            PartialView("_ZoneCubes", await api.DeviceFarmUnitZoneDashboardListGet(idDeviceFarmUnit));

        public async Task<ActionResult> Zone(int idDeviceFarmUnitZone)
        {
            ZoneViewModel model = await BuildZoneViewAsync(idDeviceFarmUnitZone);
            // Last 24h hourly buckets, only fetched here (not in the 10s-polled ZoneDetails fragment) - the chart lives outside that fragment.
            model.SensorDataJson = await api.SensorDataZoneAverageGet(idDeviceFarmUnitZone, 24, 1);
            return View(model);
        }

        public async Task<ActionResult> ZoneDetails(int idDeviceFarmUnitZone) =>
            PartialView("_ZoneDetails", await BuildZoneViewAsync(idDeviceFarmUnitZone));

        private async Task<ZoneViewModel> BuildZoneViewAsync(int idDeviceFarmUnitZone)
        {
            DeviceFarmUnitZoneDashboard dashboard = await api.DeviceFarmUnitZoneDashboardGet(idDeviceFarmUnitZone);

            // LastSeenAt is stored/served in UTC; convert here for display only.
            IList<DeviceFleetStatus> fleet = (await api.DeviceFleetGet())
                .Where(f => f.DeviceFarmUnitZoneID == idDeviceFarmUnitZone)
                .ToList();
            string? timeZone = User.GetTimeZone();
            foreach (var d in fleet)
            {
                if (d.LastSeenAt is DateTimeOffset utc)
                {
                    d.LastSeenAt = new DateTimeOffset(TimeZoneHelper.ToUserLocalTime(utc.UtcDateTime, timeZone), TimeSpan.Zero);
                }
            }

            bool hasController = dashboard.Devices.Any(d => d.DeviceControllerEnabled == true);
            // Roadmap #238 - fetched unconditionally now: a sensor-only zone still has dashboard widgets to configure, even with no automation section to show below them.
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            IList<DeviceFarmUnitZoneRule> rules = [];
            IList<DeviceManualOverride> manualOverrides = [];
            if (hasController)
            {
                rules = await api.DeviceFarmUnitZoneRulesGet(idDeviceFarmUnitZone);
                manualOverrides = await api.DeviceFarmUnitZoneManualActuateStatus(idDeviceFarmUnitZone);
            }

            // Breadcrumb's Farm segment; cheap enough to fetch every load, no need to gate behind hasController like Rules/ManualOverrides above.
            DeviceFarmUnit unit = await api.DeviceFarmUnitGet(dashboard.IDDeviceFarmUnit);

            return new ZoneViewModel
            {
                Dashboard = dashboard,
                Fleet = fleet,
                DisplayTimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
                Zone = zone,
                Rules = rules,
                ManualOverrides = manualOverrides,
                DiscoveredDevices = await api.DiscoveryResultsGet(null, idDeviceFarmUnitZone),
                WifiConfigs = await api.DiscoveryWifiConfigsGet(),
                UnitName = unit.DeviceFarmUnitName,
                UnitFarmID = unit.DeviceFarmID,
                Farms = await api.DeviceFarmsGet(),
            };
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ScanZone(DiscoveryScanRequest request)
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
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone = request.ZoneID });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegisterDiscoveredDeviceZone(DiscoveryRegisterRequest request)
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
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone = request.ZoneID });
        }

        // Roadmap #219. durationMinutes is the admin-facing unit (matches the quick-preset buttons); converted to seconds only for the wire request. TargetMetric/TargetThreshold/TargetHysteresis are ignored server-side for Duration mode and vice versa (Agrumy.Api.Commands.ManualActuateService), so posting all six fields regardless of the selected mode is harmless.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZoneManualActuateStart(int idDeviceFarmUnitZone, RelayFunction relayFunction, ManualOverrideMode mode,
            int? durationMinutes, SensorMetric? targetMetric, double? targetThreshold, double? targetHysteresis)
        {
            var request = new ManualActuateRequest(relayFunction, mode, durationMinutes is int m ? m * 60 : null, targetMetric, targetThreshold, targetHysteresis);
            try
            {
                await api.DeviceFarmUnitZoneManualActuateStart(idDeviceFarmUnitZone, request);
                TempData["Message"] = $"{relayFunction} manually started.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZoneManualActuateStop(int idDeviceFarmUnitZone, RelayFunction relayFunction)
        {
            try
            {
                await api.DeviceFarmUnitZoneManualActuateStop(idDeviceFarmUnitZone, relayFunction);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        /// Unit-level fan-out - same request shape as the Zone-level trigger above, applied to every zone's controller under this unit.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitManualActuateStart(int idDeviceFarmUnit, RelayFunction relayFunction, ManualOverrideMode mode,
            int? durationMinutes, SensorMetric? targetMetric, double? targetThreshold, double? targetHysteresis)
        {
            var request = new ManualActuateRequest(relayFunction, mode, durationMinutes is int m ? m * 60 : null, targetMetric, targetThreshold, targetHysteresis);
            try
            {
                IReadOnlyList<int> affected = await api.DeviceFarmUnitManualActuateStart(idDeviceFarmUnit, request);
                TempData["Message"] = $"{relayFunction} manually started across {affected.Count} zone(s).";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zones), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitAdd(string deviceFarmUnitName)
        {
            DeviceFarmUnit unit = await api.DeviceFarmUnitAdd(new DeviceFarmUnit { DeviceFarmUnitName = deviceFarmUnitName });
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneAdd(new DeviceFarmUnitZone { DeviceFarmUnitID = unit.IDDeviceFarmUnit!.Value, DeviceFarmUnitZoneName = "Default" });
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone = zone.IDDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitDelete(int idDeviceFarmUnit)
        {
            await api.DeviceFarmUnitDelete(idDeviceFarmUnit);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitRename(int idDeviceFarmUnit, string deviceFarmUnitName)
        {
            await api.DeviceFarmUnitUpdate(new DeviceFarmUnit { IDDeviceFarmUnit = idDeviceFarmUnit, DeviceFarmUnitName = deviceFarmUnitName });
            return RedirectToAction(nameof(Zones), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZoneAdd(int idDeviceFarmUnit, string deviceFarmUnitZoneName)
        {
            await api.DeviceFarmUnitZoneAdd(new DeviceFarmUnitZone { DeviceFarmUnitID = idDeviceFarmUnit, DeviceFarmUnitZoneName = deviceFarmUnitZoneName });
            return RedirectToAction(nameof(Zones), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZoneDelete(int idDeviceFarmUnitZone, int idDeviceFarmUnit)
        {
            await api.DeviceFarmUnitZoneDelete(idDeviceFarmUnitZone);
            return RedirectToAction(nameof(Zones), new { idDeviceFarmUnit });
        }

        // Fetch-then-patch: the update call overwrites every field unconditionally, so posting just the name would blank the other fields.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZoneRename(int idDeviceFarmUnitZone, string deviceFarmUnitZoneName)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            zone.DeviceFarmUnitZoneName = deviceFarmUnitZoneName;
            await api.DeviceFarmUnitZoneUpdate(zone);
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        // ---- Dashboard widgets (roadmap #238) - fetch-then-patch the whole list, same pattern as ZoneRename above. ----

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> WidgetAdd(int idDeviceFarmUnitZone, DashboardWidgetType type, SensorMetric? metric, RelayFunction? relayFunction, string? label)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            zone.DashboardWidgets.Add(new DashboardWidget { Type = type, Metric = metric, RelayFunction = relayFunction, Label = label });
            try
            {
                await api.DeviceFarmUnitZoneWidgetsSet(idDeviceFarmUnitZone, zone.DashboardWidgets);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> WidgetRemove(int idDeviceFarmUnitZone, int index)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            if (index >= 0 && index < zone.DashboardWidgets.Count)
            {
                zone.DashboardWidgets.RemoveAt(index);
                await api.DeviceFarmUnitZoneWidgetsSet(idDeviceFarmUnitZone, zone.DashboardWidgets);
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> WidgetMove(int idDeviceFarmUnitZone, int index, bool up)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            int target = up ? index - 1 : index + 1;
            if (index >= 0 && index < zone.DashboardWidgets.Count && target >= 0 && target < zone.DashboardWidgets.Count)
            {
                (zone.DashboardWidgets[index], zone.DashboardWidgets[target]) = (zone.DashboardWidgets[target], zone.DashboardWidgets[index]);
                await api.DeviceFarmUnitZoneWidgetsSet(idDeviceFarmUnitZone, zone.DashboardWidgets);
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SafetyLimitsUpdate(int idDeviceFarmUnitZone, int? waterPumpMaxRunSeconds, int? waterPumpCooldownSeconds, bool skipWaterPumpWhenRainPredicted,
            int? heatingMaxRunSeconds, int? ventilationMaxRunSeconds)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            zone.WaterPumpMaxRunSeconds = waterPumpMaxRunSeconds;
            zone.WaterPumpCooldownSeconds = waterPumpCooldownSeconds;
            zone.SkipWaterPumpWhenRainPredicted = skipWaterPumpWhenRainPredicted;
            zone.HeatingMaxRunSeconds = heatingMaxRunSeconds;
            zone.VentilationMaxRunSeconds = ventilationMaxRunSeconds;
            try
            {
                await api.DeviceFarmUnitZoneUpdate(zone);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        // Roadmap #234 - all three null together means "no tank tracking", the empty-string->null coercion below keeps a blank form submit from writing a zero-capacity/zero-calibration tank instead.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TankCalibrationUpdate(int idDeviceFarmUnitZone, double? tankCapacityLiters, int? waterLevelRawEmpty, int? waterLevelRawFull, double? waterPumpMinLevel)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            zone.TankCapacityLiters = tankCapacityLiters;
            zone.WaterLevelRawEmpty = waterLevelRawEmpty;
            zone.WaterLevelRawFull = waterLevelRawFull;
            zone.WaterPumpMinLevel = waterPumpMinLevel;
            try
            {
                await api.DeviceFarmUnitZoneUpdate(zone);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        // ---- Rules (Zone/Unit/Global scope, roadmap #212) ----------------------------

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RuleAdd(int idDeviceFarmUnitZone, RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, idDeviceFarmUnitZone, null), r => api.DeviceFarmUnitZoneRuleAdd(r));
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RuleDelete(int idDeviceFarmUnitZoneRule, int idDeviceFarmUnitZone)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.DeviceFarmUnitZoneRuleDelete(r));
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult> UnitRules(int idDeviceFarmUnit) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.Unit,
            ScopeId = idDeviceFarmUnit,
            Rules = await api.DeviceFarmUnitRulesGet(idDeviceFarmUnit),
            RedirectActionName = nameof(UnitRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitRuleAdd(int idDeviceFarmUnit, RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, null, idDeviceFarmUnit), r => api.DeviceFarmUnitRuleAdd(r));
            return RedirectToAction(nameof(UnitRules), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitRuleDelete(int idDeviceFarmUnitZoneRule, int idDeviceFarmUnit)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.DeviceFarmUnitRuleDelete(r));
            return RedirectToAction(nameof(UnitRules), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult> DeviceFarmRules(int idDeviceFarm) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.Farm,
            ScopeId = idDeviceFarm,
            Rules = await api.DeviceFarmRulesGet(idDeviceFarm),
            RedirectActionName = nameof(DeviceFarmRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeviceFarmRuleAdd(int idDeviceFarm, RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, null, null, idDeviceFarm), r => api.DeviceFarmRuleAdd(r));
            return RedirectToAction(nameof(DeviceFarmRules), new { idDeviceFarm });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeviceFarmRuleDelete(int idDeviceFarmUnitZoneRule, int idDeviceFarm)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.DeviceFarmRuleDelete(r));
            return RedirectToAction(nameof(DeviceFarmRules), new { idDeviceFarm });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult> GlobalRules() => View(new RuleEditorViewModel
        {
            Scope = RuleScope.Global,
            Rules = await api.GlobalRulesGet(),
            RedirectActionName = nameof(GlobalRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GlobalRuleAdd(RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, null, null), r => api.GlobalRuleAdd(r));
            return RedirectToAction(nameof(GlobalRules));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GlobalRuleDelete(int idDeviceFarmUnitZoneRule)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.GlobalRuleDelete(r));
            return RedirectToAction(nameof(GlobalRules));
        }

        private async Task AddRuleAsync(DeviceFarmUnitZoneRule rule, Func<DeviceFarmUnitZoneRule, Task<int>> add)
        {
            try
            {
                await add(rule);
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

        /// Builds a DeviceFarmUnitZoneRule from the form input - exactly one of idDeviceFarmUnitZone/idDeviceFarmUnit is non-null for Zone/Unit scope, both null for Global. RootConditionJson comes pre-built from wwwroot/js/rule-builder.js (roadmap #396(4)) - must deserialize with ConditionConfigJson.Options, the options-less overload would misread the camelCase JS produced.
        private static DeviceFarmUnitZoneRule BuildRule(RuleFormInput input, int? idDeviceFarmUnitZone, int? idDeviceFarmUnit, int? idDeviceFarm = null)
        {
            ConditionNode? root = string.IsNullOrWhiteSpace(input.RootConditionJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<ConditionNode>(input.RootConditionJson, ConditionConfigJson.Options);
            return new DeviceFarmUnitZoneRule
            {
                DeviceFarmUnitZoneID = idDeviceFarmUnitZone,
                DeviceFarmUnitID = idDeviceFarmUnit,
                DeviceFarmID = idDeviceFarm,
                ActionType = input.ActionType,
                RelayFunction = input.ActionType == ActionType.Relay ? input.RelayFunction : null,
                Name = input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                IsSafetyRule = input.IsSafetyRule,
                NotificationSubject = input.ActionType == ActionType.Notification ? input.NotificationSubject : null,
                NotificationBody = input.ActionType == ActionType.Notification ? input.NotificationBody : null,
                Root = root,
            };
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult> AssignPicker(int idDeviceFarmUnitZone, bool controllerCapable) =>
            View(new AssignPickerViewModel
            {
                IDDeviceFarmUnitZone = idDeviceFarmUnitZone,
                ControllerCapable = controllerCapable,
                Devices = await api.DeviceUnassignedGet(controllerCapable),
            });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Assign(int idDevice, int idDeviceFarmUnitZone, bool controllerCapable)
        {
            try
            {
                await api.DeviceAssign(new DeviceZoneAssignment { IDDevice = idDevice, IDDeviceFarmUnitZone = idDeviceFarmUnitZone });
            }
            catch (ApiException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Body);
                return View(nameof(AssignPicker), new AssignPickerViewModel
                {
                    IDDeviceFarmUnitZone = idDeviceFarmUnitZone,
                    ControllerCapable = controllerCapable,
                    Devices = await api.DeviceUnassignedGet(controllerCapable),
                });
            }

            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Remove(int idDevice, int idDeviceFarmUnitZone)
        {
            await api.DeviceUnassign(idDevice);
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        // returnUrl comes from window.location client-side (_ZoneStatusBadge) since Request.Path server-side would be the AJAX poll endpoint, not the visible page; falls back to Index if missing/unsafe.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AcknowledgeAlert(int idEventDevice, string? returnUrl)
        {
            await api.DeviceEventAcknowledge(idEventDevice);
            return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
        }
    }
}
