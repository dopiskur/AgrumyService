using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    public class UnitZonesViewModel
    {
        public DeviceFarmUnit Unit { get; set; } = new();
        // Breadcrumb only grows a Farm segment once there's a second one (Unit.DeviceFarmID alone tells the view which one, if any).
        public IList<DeviceFarm> Farms { get; set; } = [];
        public IList<DeviceFarmUnitZoneDashboard> Zones { get; set; } = new List<DeviceFarmUnitZoneDashboard>();
        public string DisplayTimeZone { get; set; } = "UTC";
        public IList<DiscoveryResult> DiscoveredDevices { get; set; } = new List<DiscoveryResult>();
        public IList<TenantWifiConfig> WifiConfigs { get; set; } = new List<TenantWifiConfig>();

        /// {"sensorData":[...]} averaged across every device in this unit (all its zones) - see SensorDataUnitAverageGetAsync.
        public string? SensorDataJson { get; set; }
    }
}
