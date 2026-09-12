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
    public partial class DeviceFarmUnitController(IApi api) : Controller
    {
        // ---- Dashboard widget wizard ------------------------------------

        /// The old Unit/Zone cube overview lives on Farms now; this route is the guided flow for building a zone's (or a parcel's) custom dashboard. Picking a zone/parcel here only chooses WHICH page you're editing - each widget added on it independently picks its own Farm/Unit/Zone data source, it is no longer a dashboard-wide scope every widget shares.
        public async Task<ActionResult> Index(int? idDeviceFarmUnitZone, int? idFarmParcelZone)
        {
            IList<ZoneOption> zones = await BuildZoneOptionsAsync();
            IList<ParcelOption> parcels = await BuildParcelOptionsAsync();
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            IList<DeviceFarmUnit> units = await api.DeviceFarmUnitsGet();
            DashboardWidgetsViewModel? selected = null;
            if (idDeviceFarmUnitZone is int zoneId && zones.Any(z => z.IDDeviceFarmUnitZone == zoneId))
            {
                selected = await BuildDashboardWidgetsViewModelAsync(zoneId, zones, farms, units);
            }
            else if (idFarmParcelZone is int parcelId && parcels.Any(p => p.IDFarmParcelZone == parcelId))
            {
                selected = await BuildParcelDashboardWidgetsViewModelAsync(parcelId, zones, farms, units);
            }
            return View(new DashboardWizardViewModel
            {
                Zones = zones,
                Parcels = parcels,
                Farms = farms,
                Units = units,
                CanManage = hasAnyRole(RoleNames.DeviceManagers),
                SelectedZoneId = idDeviceFarmUnitZone,
                SelectedParcelId = idFarmParcelZone,
                Selected = selected,
            });
        }

        private async Task<DashboardWidgetsViewModel> BuildDashboardWidgetsViewModelAsync(int zoneId, IList<ZoneOption> zones, IList<DeviceFarm> farms, IList<DeviceFarmUnit> units)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(zoneId);
            DeviceFarmUnitZoneDashboard dashboard = await api.DeviceFarmUnitZoneDashboardGet(zoneId);
            var ctx = await BuildWidgetContextAsync(zone.DashboardWidgets, zones, farms, units);

            return new DashboardWidgetsViewModel
            {
                Leaf = zone,
                Dashboard = dashboard,
                Fleet = ctx.Fleet,
                CanManage = hasAnyRole(RoleNames.DeviceManagers),
                Farms = ctx.Farms,
                Units = ctx.Units,
                Zones = ctx.Zones,
                WidgetData = ctx.WidgetData,
                AlertStatusData = ctx.AlertStatusData,
            };
        }

        /// Parcel's equivalent of BuildDashboardWidgetsViewModelAsync - Dashboard has no Parcel-shaped equivalent consumer (see DashboardWidgetsViewModel.Dashboard's own remarks: unused by _DashboardWidgets.cshtml), so a placeholder is enough.
        private async Task<DashboardWidgetsViewModel> BuildParcelDashboardWidgetsViewModelAsync(int parcelId, IList<ZoneOption> zones, IList<DeviceFarm> farms, IList<DeviceFarmUnit> units)
        {
            FarmParcelZone parcel = await api.ParcelGetById(parcelId);
            var ctx = await BuildWidgetContextAsync(parcel.DashboardWidgets, zones, farms, units);

            return new DashboardWidgetsViewModel
            {
                Leaf = parcel,
                IsParcelLeaf = true,
                Dashboard = new DeviceFarmUnitZoneDashboard(),
                Fleet = ctx.Fleet,
                CanManage = hasAnyRole(RoleNames.DeviceManagers),
                Farms = ctx.Farms,
                Units = ctx.Units,
                Zones = ctx.Zones,
                WidgetData = ctx.WidgetData,
                AlertStatusData = ctx.AlertStatusData,
            };
        }

        /// Shared by BuildDashboardWidgetsViewModelAsync (Index wizard) and BuildZoneViewAsync (Zone page) - one DashboardAggregate/AlertStatus fetch per distinct target a zone's widgets reference, not one per widget, since several widgets commonly share the same target (e.g. two metrics for the same Farm).
        private async Task<(IList<DeviceFarm> Farms, IList<DeviceFarmUnit> Units, IList<ZoneOption> Zones, IList<DeviceFleetStatus> Fleet,
            IReadOnlyDictionary<(HierarchyNodeKind, int), DashboardAggregate> WidgetData,
            IReadOnlyDictionary<(NotificationEventType, HierarchyNodeKind, int), bool> AlertStatusData)>
            BuildWidgetContextAsync(IList<DashboardWidget> widgets, IList<ZoneOption>? zones = null, IList<DeviceFarm>? farms = null, IList<DeviceFarmUnit>? units = null)
        {
            zones ??= await BuildZoneOptionsAsync();
            farms ??= await api.DeviceFarmsGet();
            units ??= await api.DeviceFarmUnitsGet();
            IList<DeviceFleetStatus> fleet = await api.DeviceFleetGet();

            var widgetData = new Dictionary<(HierarchyNodeKind, int), DashboardAggregate>();
            var alertStatusData = new Dictionary<(NotificationEventType, HierarchyNodeKind, int), bool>();
            foreach (DashboardWidget w in widgets)
            {
                if (w.AggregationLevel is not HierarchyNodeKind level || w.LevelID is not int levelId)
                {
                    continue;
                }
                if (w.Type is DashboardWidgetType.SensorValue or DashboardWidgetType.SensorTrend && !widgetData.ContainsKey((level, levelId)))
                {
                    widgetData[(level, levelId)] = await api.DeviceFarmUnitDashboardWidgetAggregateGet(level, levelId);
                }
                else if (w.Type == DashboardWidgetType.AlertStatus && w.AlertEventType is NotificationEventType eventType && !alertStatusData.ContainsKey((eventType, level, levelId)))
                {
                    alertStatusData[(eventType, level, levelId)] = (await api.DeviceFarmUnitDashboardAlertStatusGet(eventType, level, levelId)).IsActive;
                }
            }
            return (farms, units, zones, fleet, widgetData, alertStatusData);
        }

        private bool hasAnyRole(string csv) => csv.Split(',').Any(User.IsInRole);

        /// Open-Field's equivalent of BuildZoneOptionsAsync - flattens every parcel across every crop into one Crop-labeled list for the wizard's parcel picker.
        private async Task<IList<ParcelOption>> BuildParcelOptionsAsync()
        {
            IList<Sowing> crops = await api.CropsGet();
            var options = new List<ParcelOption>();
            foreach (Sowing crop in crops)
            {
                if (crop.IDSowing is not int cropId) { continue; }
                foreach (FarmParcelZone parcel in await api.ParcelsGet(cropId))
                {
                    options.Add(new ParcelOption { IDFarmParcelZone = parcel.IDFarmParcelZone!.Value, ParcelName = parcel.FarmParcelZoneName ?? "", GroupLabel = crop.SowingName ?? "" });
                }
            }
            return options;
        }

        /// Flattens every zone across every unit into one Farm/Unit-labeled list for the wizard's zone picker - no single API call returns this shape, so it composes DeviceFarmsGet+DeviceFarmUnitDashboardGet+per-unit DeviceFarmUnitZonesGet.
        private async Task<IList<ZoneOption>> BuildZoneOptionsAsync()
        {
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            IList<DeviceFarmUnitDashboard> units = await api.DeviceFarmUnitDashboardGet();
            var options = new List<ZoneOption>();
            foreach (DeviceFarmUnitDashboard unit in units)
            {
                string? farmName = unit.DeviceFarmID is int farmId ? farms.FirstOrDefault(f => f.IDDeviceFarm == farmId)?.DeviceFarmName : null;
                string groupLabel = farmName is null ? unit.DeviceFarmUnitName ?? "" : $"{farmName} / {unit.DeviceFarmUnitName}";
                foreach (DeviceFarmUnitZone zone in await api.DeviceFarmUnitZonesGet(unit.IDDeviceFarmUnit))
                {
                    options.Add(new ZoneOption { IDDeviceFarmUnitZone = zone.IDDeviceFarmUnitZone!.Value, ZoneName = zone.DeviceFarmUnitZoneName ?? "", GroupLabel = groupLabel });
                }
            }
            return options;
        }

        // ---- Farm --------------------------------------

        // Shared by the full page and its 10s-polled fragment (IndexCubes below) so a live update never reverts the farm grouping.
        private async Task<GroupedUnitCubesViewModel> BuildGroupedUnitCubesAsync() => new()
        {
            Units = await api.DeviceFarmUnitDashboardGet(),
            Farms = await api.DeviceFarmsGet(),
            Crops = await api.CropDashboardGet(),
            Openfields = await api.FarmOpenfieldsGet(),
        };

        // The Unit/Zone cube overview moved here from the old Dashboard (the wizard took that route over).
        public async Task<ActionResult> Farms() => View(new FarmListViewModel
        {
            Farms = await api.DeviceFarmsGet(),
            Units = await api.DeviceFarmUnitDashboardGet(),
            Crops = await api.CropDashboardGet(),
            Openfields = await api.FarmOpenfieldsGet(),
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
        public async Task<ActionResult> FarmOpenfieldAdd(string deviceFarmName)
        {
            await api.FarmOpenfieldCreate(deviceFarmName);
            return RedirectToAction("Index", "FarmOpenfield");
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmRename(int idDeviceFarm, string deviceFarmName)
        {
            await api.DeviceFarmUpdate(new DeviceFarm { IDDeviceFarm = idDeviceFarm, DeviceFarmName = deviceFarmName });
            return RedirectToAction(nameof(Farms));
        }

        /// Called via fetch from farms-reorder.js right after a drag ends, not a form post - the whole Farms page is a 10s live-refresh target so a full-page redirect would fight the next poll.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmsReorder([FromBody] List<int> orderedFarmIds)
        {
            try
            {
                await api.DeviceFarmsReorder(orderedFarmIds);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> FarmDelete(int idDeviceFarm)
        {
            await api.DeviceFarmDelete(idDeviceFarm);
            return RedirectToAction(nameof(Farms));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ScanFarm(DiscoveryScanRequest request)
        {
            try
            {
                await api.DiscoveryScan(request);
                TempData["Message"] = "Scan started - discovered devices will appear on each unit/zone's own page shortly.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Farms));
        }

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> DeviceFarmManualActuate(int idDeviceFarm)
        {
            DeviceFarm farm = await api.DeviceFarmGet(idDeviceFarm);
            return View(new DeviceFarmManualActuateViewModel
            {
                Farm = farm,
                Heating = new ManualActuateFunctionViewModel
                {
                    ScopeId = idDeviceFarm, IsFarmLevel = true, RelayFunction = RelayFunction.Heating, Label = "Heating",
                    AllowedTargetMetrics = [SensorMetric.Temperature],
                },
                Ventilation = new ManualActuateFunctionViewModel
                {
                    ScopeId = idDeviceFarm, IsFarmLevel = true, RelayFunction = RelayFunction.Ventilation, Label = "Ventilation",
                    AllowedTargetMetrics = [SensorMetric.Temperature, SensorMetric.Humidity],
                },
                Irrigation = new ManualActuateFunctionViewModel
                {
                    ScopeId = idDeviceFarm, IsFarmLevel = true, RelayFunction = RelayFunction.WaterPump, Label = "Irrigation",
                    AllowedTargetMetrics = [SensorMetric.Moisture],
                },
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeviceFarmManualActuateStart(int idDeviceFarm, RelayFunction relayFunction, ManualOverrideMode mode,
            int? durationMinutes, SensorMetric? targetMetric, double? targetThreshold, double? targetHysteresis)
        {
            var request = new ManualActuateRequest(relayFunction, mode, durationMinutes is int m ? m * 60 : null, targetMetric, targetThreshold, targetHysteresis);
            try
            {
                IReadOnlyList<int> affected = await api.DeviceFarmManualActuateStart(idDeviceFarm, request);
                TempData["Message"] = $"{relayFunction} manually started across {affected.Count} zone(s).";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(DeviceFarmManualActuate), new { idDeviceFarm });
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

        /// The "migrate first" offer in FarmDelete's confirmation flow: every unit still on idDeviceFarm moves to idTargetFarm, one at a time (same UnitAssignFarm write, just looped) - called before FarmDelete, never together with it in one request.
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
    }
}
