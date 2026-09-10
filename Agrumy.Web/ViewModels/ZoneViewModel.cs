using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    public class ZoneViewModel
    {
        public required DeviceFarmUnitZoneDashboard Dashboard { get; init; }
        public IList<DeviceFleetStatus> Fleet { get; init; } = [];
        public string DisplayTimeZone { get; init; } = "UTC";

        /// {"sensorData":[...]} averaged across the zone's devices (SensorDataZoneAverageGetAsync); only set on the full Zone page, not the polled ZoneDetails fragment, so it isn't re-queried on every poll.
        public string? SensorDataJson { get; set; }

        // Null/empty when the zone has no controller-capable device assigned; the automation section is not shown at all in that case.
        public DeviceFarmUnitZone? Zone { get; init; }
        public IList<DeviceFarmUnitZoneRule> Rules { get; init; } = [];

        /// Roadmap #219 - currently-active manual commands (not yet past ExpiresAtUtc), same "no controller, no section" condition as Zone/Rules above.
        public IList<DeviceManualOverride> ManualOverrides { get; init; } = [];

        // Same "no controller, no section" gating as Rules/ManualOverrides above - a sensor-only zone has nothing to apply a relay-driving template to.
        public IList<HorticultureCatalogEntry> CropCatalog { get; init; } = [];
        public IList<HorticultureCatalogEntry> PermaCatalog { get; init; } = [];
        public IList<HorticultureCatalogEntry> HydroponicCatalog { get; init; } = [];
        public IList<HorticultureCatalogEntry> FruitCatalog { get; init; } = [];

        public IList<DiscoveryResult> DiscoveredDevices { get; set; } = [];
        public IList<TenantWifiConfig> WifiConfigs { get; set; } = [];

        // Breadcrumb only grows a Farm segment once there's a second one.
        public string? UnitName { get; init; }
        public int? UnitFarmID { get; init; }
        public IList<DeviceFarm> Farms { get; init; } = [];

        // ---- Dashboard widget rendering context - Fleet above stays zone-scoped for the page's own device list, these are separate since a widget here can target a different zone/unit/farm. ----
        public IList<DeviceFleetStatus> AllFleet { get; init; } = [];
        public IList<DeviceFarmUnit> Units { get; init; } = [];
        public IList<ZoneOption> Zones { get; init; } = [];
        public IReadOnlyDictionary<(DashboardAggregationLevel Level, int LevelId), DashboardAggregate> WidgetData { get; init; } =
            new Dictionary<(DashboardAggregationLevel, int), DashboardAggregate>();
    }
}
