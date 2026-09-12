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
        // ---- Dashboard widgets - fetch-then-patch the whole list, same pattern as ZoneRename above. ----

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> WidgetAdd(int idDeviceFarmUnitZone, DashboardWidgetType type, SensorMetric? metric, RelayFunction? relayFunction, HierarchyNodeKind? aggregationLevel, int? levelId, string? label)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            // RelayStatus's target is always a zone - the form's own "which zone" picker feeds levelId the same as a sensor widget's Zone-level target does.
            zone.DashboardWidgets.Add(new DashboardWidget
            {
                Type = type,
                Metric = metric,
                RelayFunction = relayFunction,
                AggregationLevel = type == DashboardWidgetType.RelayStatus ? HierarchyNodeKind.Zone : aggregationLevel,
                LevelID = levelId,
                Label = label,
            });
            try
            {
                await api.DeviceFarmUnitZoneWidgetsSet(idDeviceFarmUnitZone, zone.DashboardWidgets);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index), new { idDeviceFarmUnitZone });
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
            return RedirectToAction(nameof(Index), new { idDeviceFarmUnitZone });
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
            return RedirectToAction(nameof(Index), new { idDeviceFarmUnitZone });
        }

        /// Drag-and-drop reorder, same "POST the whole new order" idiom as farms-reorder.js, but the order carries OLD LIST INDICES (widgets have no id of their own) rather than entity ids.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> WidgetsReorder(int idDeviceFarmUnitZone, [FromBody] List<int> order)
        {
            try
            {
                DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
                if (!IsValidReorder(order, zone.DashboardWidgets.Count))
                {
                    return BadRequest();
                }
                zone.DashboardWidgets = order.Select(i => zone.DashboardWidgets[i]).ToList();
                await api.DeviceFarmUnitZoneWidgetsSet(idDeviceFarmUnitZone, zone.DashboardWidgets);
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
        public async Task<ActionResult> WidgetGridColumnsSet(int idDeviceFarmUnitZone, int columns)
        {
            await api.DeviceFarmUnitZoneGridColumnsSet(idDeviceFarmUnitZone, columns);
            return RedirectToAction(nameof(Index), new { idDeviceFarmUnitZone });
        }

        /// Statistics branch: one tile per (metric x scope) combination. Alerting branch: one status box per (alert type x scope) combination. Targets can be Farms/Units/Zones/Parcels mixed - ResolveWizardTargetsAsync fans a Farm/Unit out to every Zone/Parcel dashboard underneath it, then the same widget set is fetch-then-patched onto each one independently (one failure doesn't block the rest).
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> WidgetWizardAddMulti(List<string>? targets, string branch, List<int>? metrics, List<int>? alertTypes, List<string>? scopes, DashboardWidgetType chartType)
        {
            (List<int> zoneIds, List<int> parcelIds) = await ResolveWizardTargetsAsync(targets);
            List<DashboardWidget> widgets = BuildWizardWidgets(branch, metrics, alertTypes, scopes, chartType);

            int failures = 0;
            foreach (int zoneId in zoneIds)
            {
                try
                {
                    DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(zoneId);
                    zone.DashboardWidgets.AddRange(widgets);
                    await api.DeviceFarmUnitZoneWidgetsSet(zoneId, zone.DashboardWidgets);
                }
                catch (ApiException) { failures++; }
            }
            foreach (int parcelId in parcelIds)
            {
                try
                {
                    FarmParcelZone parcel = await api.ParcelGetById(parcelId);
                    parcel.DashboardWidgets.AddRange(widgets);
                    await api.ParcelWidgetsSet(parcelId, parcel.DashboardWidgets);
                }
                catch (ApiException) { failures++; }
            }
            if (failures > 0)
            {
                TempData["Error"] = $"Added to {zoneIds.Count + parcelIds.Count - failures} of {zoneIds.Count + parcelIds.Count} dashboard(s) - {failures} failed.";
            }
            return zoneIds.Count > 0
                ? RedirectToAction(nameof(Index), new { idDeviceFarmUnitZone = zoneIds[0] })
                : RedirectToAction(nameof(Index), new { idFarmParcelZone = parcelIds.Count > 0 ? parcelIds[0] : (int?)null });
        }

        /// Expands the wizard's target picker ("targets" - Farm/Unit/Zone/FarmParcelZone kind:id pairs, same encoding as _DashboardWizardScopePicker's "scopes") into the concrete Zone/Parcel dashboards those widgets actually land on - a Unit fans out to its Zones, a Farm to every Unit's Zones plus (Open-Field) every Parcel under its FarmOpenfield.
        private async Task<(List<int> ZoneIds, List<int> ParcelIds)> ResolveWizardTargetsAsync(List<string>? targets)
        {
            var zoneIds = new HashSet<int>();
            var parcelIds = new HashSet<int>();
            foreach (string t in targets ?? [])
            {
                string[] parts = t.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int kindInt) || !int.TryParse(parts[1], out int id))
                {
                    continue;
                }
                switch ((HierarchyNodeKind)kindInt)
                {
                    case HierarchyNodeKind.Zone:
                        zoneIds.Add(id);
                        break;
                    case HierarchyNodeKind.FarmParcelZone:
                        parcelIds.Add(id);
                        break;
                    case HierarchyNodeKind.Unit:
                        foreach (DeviceFarmUnitZone z in await api.DeviceFarmUnitZonesGet(id))
                        {
                            zoneIds.Add(z.IDDeviceFarmUnitZone!.Value);
                        }
                        break;
                    case HierarchyNodeKind.Farm:
                        foreach (DeviceFarmUnit unit in (await api.DeviceFarmUnitsGet()).Where(u => u.DeviceFarmID == id))
                        {
                            foreach (DeviceFarmUnitZone z in await api.DeviceFarmUnitZonesGet(unit.IDDeviceFarmUnit))
                            {
                                zoneIds.Add(z.IDDeviceFarmUnitZone!.Value);
                            }
                        }
                        foreach (FarmOpenfield openfield in (await api.FarmOpenfieldsGet()).Where(o => o.FarmID == id))
                        {
                            foreach (FarmParcel parcel in await api.FarmParcelsGet(openfield.IDFarmOpenfield!.Value))
                            {
                                foreach (FarmParcelZone z in await api.FarmParcelZonesGet(parcel.IDFarmParcel!.Value))
                                {
                                    parcelIds.Add(z.IDFarmParcelZone!.Value);
                                }
                            }
                        }
                        break;
                }
            }
            return (zoneIds.ToList(), parcelIds.ToList());
        }

        // ---- Dashboard widgets, Open-Field's equivalent - same fetch-then-patch pattern as WidgetAdd/Remove/Move above. ----

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelWidgetAdd(int idFarmParcelZone, DashboardWidgetType type, SensorMetric? metric, RelayFunction? relayFunction, HierarchyNodeKind? aggregationLevel, int? levelId, string? label)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
            parcel.DashboardWidgets.Add(new DashboardWidget
            {
                Type = type,
                Metric = metric,
                RelayFunction = relayFunction,
                AggregationLevel = type == DashboardWidgetType.RelayStatus ? HierarchyNodeKind.Zone : aggregationLevel,
                LevelID = levelId,
                Label = label,
            });
            try
            {
                await api.ParcelWidgetsSet(idFarmParcelZone, parcel.DashboardWidgets);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelWidgetRemove(int idFarmParcelZone, int index)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
            if (index >= 0 && index < parcel.DashboardWidgets.Count)
            {
                parcel.DashboardWidgets.RemoveAt(index);
                await api.ParcelWidgetsSet(idFarmParcelZone, parcel.DashboardWidgets);
            }
            return RedirectToAction(nameof(Index), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelWidgetMove(int idFarmParcelZone, int index, bool up)
        {
            FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
            int target = up ? index - 1 : index + 1;
            if (index >= 0 && index < parcel.DashboardWidgets.Count && target >= 0 && target < parcel.DashboardWidgets.Count)
            {
                (parcel.DashboardWidgets[index], parcel.DashboardWidgets[target]) = (parcel.DashboardWidgets[target], parcel.DashboardWidgets[index]);
                await api.ParcelWidgetsSet(idFarmParcelZone, parcel.DashboardWidgets);
            }
            return RedirectToAction(nameof(Index), new { idFarmParcelZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ParcelWidgetsReorder(int idFarmParcelZone, [FromBody] List<int> order)
        {
            try
            {
                FarmParcelZone parcel = await api.ParcelGetById(idFarmParcelZone);
                if (!IsValidReorder(order, parcel.DashboardWidgets.Count))
                {
                    return BadRequest();
                }
                parcel.DashboardWidgets = order.Select(i => parcel.DashboardWidgets[i]).ToList();
                await api.ParcelWidgetsSet(idFarmParcelZone, parcel.DashboardWidgets);
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
        public async Task<ActionResult> ParcelGridColumnsSet(int idFarmParcelZone, int columns)
        {
            await api.ParcelGridColumnsSet(idFarmParcelZone, columns);
            return RedirectToAction(nameof(Index), new { idFarmParcelZone });
        }

        /// True only for an order that is exactly a permutation of 0..count-1 - anything else (stale client state, tampered payload) is dropped rather than partially applied.
        private static bool IsValidReorder(List<int>? order, int count) =>
            order != null && order.Count == count && order.Distinct().Count() == count && order.All(i => i >= 0 && i < count);

        /// Statistics branch: metric x scope cross product - one widget per combination, not one multi-series widget, so this reuses the existing single-metric DashboardWidget model unchanged. Alerting branch: alert type x scope cross product. "scopes" entries are "level:id" pairs from _DashboardWizardScopePicker.
        private static List<DashboardWidget> BuildWizardWidgets(string branch, List<int>? metrics, List<int>? alertTypes, List<string>? scopes, DashboardWidgetType chartType)
        {
            var parsedScopes = new List<(HierarchyNodeKind Level, int Id)>();
            foreach (string s in scopes ?? [])
            {
                string[] parts = s.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int levelInt) && int.TryParse(parts[1], out int id))
                {
                    parsedScopes.Add(((HierarchyNodeKind)levelInt, id));
                }
            }

            var widgets = new List<DashboardWidget>();
            if (string.Equals(branch, "alerting", StringComparison.OrdinalIgnoreCase))
            {
                foreach (int alertType in alertTypes ?? [])
                {
                    foreach (var scope in parsedScopes)
                    {
                        widgets.Add(new DashboardWidget { Type = DashboardWidgetType.AlertStatus, AlertEventType = (NotificationEventType)alertType, AggregationLevel = scope.Level, LevelID = scope.Id });
                    }
                }
            }
            else
            {
                foreach (int metric in metrics ?? [])
                {
                    foreach (var scope in parsedScopes)
                    {
                        widgets.Add(new DashboardWidget { Type = chartType, Metric = (SensorMetric)metric, AggregationLevel = scope.Level, LevelID = scope.Id });
                    }
                }
            }
            return widgets;
        }
    }
}
