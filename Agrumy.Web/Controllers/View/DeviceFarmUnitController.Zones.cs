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
    public partial class DeviceFarmUnitController
    {
        public async Task<ActionResult> ZonesCubes(int idDeviceFarmUnit) =>
            PartialView("_ZoneCubes", await api.DeviceFarmUnitZoneDashboardListGet(idDeviceFarmUnit));

        public async Task<ActionResult> Zone(int idDeviceFarmUnitZone)
        {
            ZoneViewModel model = await BuildZoneViewAsync(idDeviceFarmUnitZone);
            // Last 24 days, hourly buckets - only fetched here (not in the 10s-polled ZoneDetails fragment), the chart lives outside that fragment.
            model.SensorDataJson = await api.SensorDataZoneAverageGet(idDeviceFarmUnitZone, DateTimeOffset.UtcNow.AddDays(-24), DateTimeOffset.UtcNow, SensorDataBucket.Hour);
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
            // Fetched unconditionally now: a sensor-only zone still has dashboard widgets to configure, even with no automation section to show below them.
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            IList<DeviceFarmUnitZoneRule> rules = [];
            IList<DeviceManualOverride> manualOverrides = [];
            IList<HorticultureCatalogEntry> cropCatalog = [];
            IList<HorticultureCatalogEntry> permaCatalog = [];
            IList<HorticultureCatalogEntry> hydroponicCatalog = [];
            IList<HorticultureCatalogEntry> fruitCatalog = [];
            if (hasController)
            {
                rules = await api.DeviceFarmUnitZoneRulesGet(idDeviceFarmUnitZone);
                manualOverrides = await api.DeviceFarmUnitZoneManualActuateStatus(idDeviceFarmUnitZone);
                cropCatalog = await api.HorticultureCatalogGet(HorticultureCatalogType.Crop);
                permaCatalog = await api.HorticultureCatalogGet(HorticultureCatalogType.Perma);
                hydroponicCatalog = await api.HorticultureCatalogGet(HorticultureCatalogType.Hydroponic);
                fruitCatalog = await api.HorticultureCatalogGet(HorticultureCatalogType.Fruit);
            }

            // Breadcrumb's Farm segment; cheap enough to fetch every load, no need to gate behind hasController like Rules/ManualOverrides above.
            DeviceFarmUnit unit = await api.DeviceFarmUnitGet(dashboard.IDDeviceFarmUnit);

            var ctx = await BuildWidgetContextAsync(zone.DashboardWidgets);

            ZonePlanting? activePlanting = await api.ZonePlantingActiveGet(idDeviceFarmUnitZone);
            IList<FieldLogEntry> fieldLog = activePlanting?.IDZonePlanting is int idActivePlanting
                ? await api.ZonePlantingFieldLogGet(idActivePlanting)
                : [];
            DateOnly? earliestHarvestDate = activePlanting?.IDZonePlanting is int idForPhi
                ? await api.ZonePlantingEarliestHarvestDateGet(idForPhi)
                : null;

            return new ZoneViewModel
            {
                Dashboard = dashboard,
                Fleet = fleet,
                DisplayTimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
                Zone = zone,
                Rules = rules,
                ManualOverrides = manualOverrides,
                CropCatalog = cropCatalog,
                PermaCatalog = permaCatalog,
                HydroponicCatalog = hydroponicCatalog,
                FruitCatalog = fruitCatalog,
                DiscoveredDevices = await api.DiscoveryResultsGet(null, idDeviceFarmUnitZone),
                WifiConfigs = await api.DiscoveryWifiConfigsGet(),
                UnitName = unit.DeviceFarmUnitName,
                UnitFarmID = unit.DeviceFarmID,
                Farms = ctx.Farms,
                AllFleet = ctx.Fleet,
                Units = ctx.Units,
                Zones = ctx.Zones,
                WidgetData = ctx.WidgetData,
                AlertStatusData = ctx.AlertStatusData,
                ActivePlanting = activePlanting,
                PlantingHistory = await api.ZonePlantingHistoryGet(idDeviceFarmUnitZone),
                FieldLog = fieldLog,
                EarliestHarvestDate = earliestHarvestDate,
            };
        }

        // ---- Greenhouse zonePlanting ciklusi + dnevnik po zoni (D8/D13) -----------------------------------

        /// "Start planting" (D8) - crop name is resolved/created against the same catalog Sowing uses (D12).
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZonePlantingStart(int idDeviceFarmUnitZone, string cropName, DateOnly plantedDate, int expectedDurationDays)
        {
            try
            {
                await api.ZonePlantingStart(idDeviceFarmUnitZone, new ZonePlantingStartRequest { CropName = cropName, PlantedDate = plantedDate, ExpectedDurationDays = expectedDurationDays });
                TempData["Message"] = "Planting started.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        /// "Close planting" (D8/D13) - same karenca-confirm gate as FarmOpenfieldController.SowingClose.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZonePlantingClose(int idDeviceFarmUnitZone, double yieldKg, double? moisturePercent, string? qualityGrade, string? note, bool confirmEarlyHarvest)
        {
            try
            {
                await api.ZonePlantingClose(idDeviceFarmUnitZone, new ZonePlantingCloseRequest { YieldKg = yieldKg, MoisturePercent = moisturePercent, QualityGrade = qualityGrade, Note = note, Confirm = confirmEarlyHarvest });
                TempData["Message"] = "Planting closed.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.StatusCode == 409
                    ? "Harvest is before the pre-harvest interval (PHI) has passed - check the confirmation box to proceed anyway."
                    : ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZonePlantingFieldLogAdd(int idDeviceFarmUnitZone, int idZonePlanting, FieldLogEntryFormInput input)
        {
            var entry = new FieldLogEntry
            {
                ZonePlantingID = idZonePlanting,
                EntryType = input.EntryType,
                DateUtc = new DateTimeOffset(input.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                Note = input.Note,
                PayloadJson = FarmOpenfieldController.BuildPayloadJson(input),
            };
            try
            {
                await api.ZonePlantingFieldLogAdd(entry);
                TempData["Message"] = $"{input.EntryType} logged.";
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
        public async Task<ActionResult> ZonePlantingFieldLogDelete(int idFieldLogEntry, int idDeviceFarmUnitZone)
        {
            await api.ZonePlantingFieldLogDelete(idFieldLogEntry);
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ApplyHorticultureCatalog(int idDeviceFarmUnitZone, HorticultureCatalogType catalogType, int catalogId)
        {
            HorticultureCatalogApplyResult result = await api.HorticultureCatalogApplyToZone(idDeviceFarmUnitZone, catalogType, catalogId);
            TempData["Message"] = result.RulesSkipped.Count == 0
                ? $"Added {result.RulesAdded} rule(s) from the catalog template."
                : $"Added {result.RulesAdded} rule(s); skipped {result.RulesSkipped.Count} (zone's rule limit reached): {string.Join(", ", result.RulesSkipped)}.";
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        /// Day/night start/end come in as plain HH:mm time-of-day fields; converted to seconds-since-midnight here so the API only ever deals with the same Schedule-node units the rest of the rule builder already uses.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DayNightPresetAdd(int idDeviceFarmUnitZone, DayNightPresetFormInput input)
        {
            var request = new DayNightTargetPresetRequest
            {
                Function = input.Function,
                Metric = input.Metric,
                Operator = input.Operator,
                DayValue = input.DayValue,
                NightValue = input.NightValue,
                Hysteresis = input.Hysteresis,
                DayStartSeconds = (int)input.DayStart.ToTimeSpan().TotalSeconds,
                DayEndSeconds = (int)input.DayEnd.ToTimeSpan().TotalSeconds,
                NamePrefix = input.NamePrefix,
            };
            try
            {
                DayNightPresetApplyResult result = await api.DayNightPresetApplyToZone(idDeviceFarmUnitZone, request);
                TempData["Message"] = result.RulesSkipped.Count == 0
                    ? $"Added {result.RulesAdded} rule(s) from the day/night preset."
                    : $"Added {result.RulesAdded} rule(s); skipped {result.RulesSkipped.Count} (zone's rule limit reached): {string.Join(", ", result.RulesSkipped)}.";
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

        //. durationMinutes is the admin-facing unit (matches the quick-preset buttons); converted to seconds only for the wire request. TargetMetric/TargetThreshold/TargetHysteresis are ignored server-side for Duration mode and vice versa (Agrumy.Api.Commands.ManualActuateService), so posting all six fields regardless of the selected mode is harmless.
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
            return RedirectToAction(nameof(UnitManualActuate), new { idDeviceFarmUnit });
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

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ZoneMigrate(int idDeviceFarmUnitZone, int idTargetDeviceFarmUnit, int idDeviceFarmUnit)
        {
            try
            {
                await api.DeviceFarmUnitZoneMigrate(idDeviceFarmUnitZone, idTargetDeviceFarmUnit);
                TempData["Message"] = "Zone migrated.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
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
    }
}
