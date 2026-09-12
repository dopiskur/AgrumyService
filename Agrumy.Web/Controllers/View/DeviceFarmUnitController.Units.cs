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
        public async Task<ActionResult> IndexCubes() => PartialView("_FarmsAndUnits", await BuildGroupedUnitCubesAsync());

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
                Units = await api.DeviceFarmUnitsGet(),
                Zones = zones,
                DisplayTimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone,
                // Last 24 days, hourly buckets - same window _ZoneDetails' sparkline trend already uses.
                SensorDataJson = await api.SensorDataUnitAverageGet(idDeviceFarmUnit, DateTimeOffset.UtcNow.AddDays(-24), DateTimeOffset.UtcNow, SensorDataBucket.Hour),
                DiscoveredDevices = await api.DiscoveryResultsGet(idDeviceFarmUnit, null),
                WifiConfigs = await api.DiscoveryWifiConfigsGet(),
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> UnitManualActuate(int idDeviceFarmUnit)
        {
            DeviceFarmUnit unit = await api.DeviceFarmUnitGet(idDeviceFarmUnit);
            return View(new UnitManualActuateViewModel
            {
                Unit = unit,
                Heating = new ManualActuateFunctionViewModel
                {
                    ScopeId = idDeviceFarmUnit, IsUnitLevel = true, RelayFunction = RelayFunction.Heating, Label = "Heating",
                    AllowedTargetMetrics = [SensorMetric.Temperature],
                },
                Ventilation = new ManualActuateFunctionViewModel
                {
                    ScopeId = idDeviceFarmUnit, IsUnitLevel = true, RelayFunction = RelayFunction.Ventilation, Label = "Ventilation",
                    AllowedTargetMetrics = [SensorMetric.Temperature, SensorMetric.Humidity],
                },
                Irrigation = new ManualActuateFunctionViewModel
                {
                    ScopeId = idDeviceFarmUnit, IsUnitLevel = true, RelayFunction = RelayFunction.WaterPump, Label = "Irrigation",
                    AllowedTargetMetrics = [SensorMetric.Moisture],
                },
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

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitAdd(string deviceFarmUnitName)
        {
            DeviceFarmUnit unit = await api.DeviceFarmUnitAdd(new DeviceFarmUnit { DeviceFarmUnitName = deviceFarmUnitName });
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneAdd(new DeviceFarmUnitZone { DeviceFarmUnitID = unit.IDDeviceFarmUnit!.Value, DeviceFarmUnitZoneName = "Default" });
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone = zone.IDDeviceFarmUnitZone });
        }

        /// Called via fetch from units-reorder.js right after a drag ends, not a form post - same reasoning as FarmsReorder above (the Farms page is a 10s live-refresh target).
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitsReorder([FromBody] List<int> orderedUnitIds)
        {
            try
            {
                await api.DeviceFarmUnitsReorder(orderedUnitIds);
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
        public async Task<ActionResult> UnitDelete(int idDeviceFarmUnit)
        {
            await api.DeviceFarmUnitDelete(idDeviceFarmUnit);
            return RedirectToAction(nameof(Farms));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitRename(int idDeviceFarmUnit, string deviceFarmUnitName)
        {
            await api.DeviceFarmUnitUpdate(new DeviceFarmUnit { IDDeviceFarmUnit = idDeviceFarmUnit, DeviceFarmUnitName = deviceFarmUnitName });
            return RedirectToAction(nameof(Zones), new { idDeviceFarmUnit });
        }
    }
}
