using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives FarmOpenfield/Parcels.cshtml - one crop's parcel list, the Open-Field mirror of UnitZonesViewModel.
    public class CropParcelsViewModel
    {
        public required Sowing Crop { get; init; }
        public required DeviceFarm Farm { get; init; }
        public IList<FarmParcelZoneDashboard> Parcels { get; init; } = [];
    }

    /// Drives FarmOpenfield/Parcel.cshtml - one parcel's detail page, the Open-Field mirror of ZoneViewModel (trimmed: no dashboard widgets or day/night presets - fast-follow).
    public class ParcelViewModel
    {
        public required FarmParcelZone Parcel { get; init; }
        /// Null when the zone currently has no active sowing (CurrentSowingID null) - D3/D4, a zone can genuinely be unoccupied between sowings.
        public Sowing? Crop { get; init; }
        public required DeviceFarm Farm { get; init; }
        public IList<DeviceFleetStatus> Devices { get; init; } = [];
        public IList<DeviceFarmUnitZoneRule> Rules { get; init; } = [];
        public IList<DeviceManualOverride> ManualOverrides { get; init; } = [];
        public string DisplayTimeZone { get; init; } = "UTC";
        public IList<DiscoveryResult> DiscoveredDevices { get; init; } = [];
        public IList<TenantWifiConfig> WifiConfigs { get; init; } = [];
    }

    /// Drives FarmOpenfield/ParcelAssignPicker.cshtml - the Open-Field mirror of AssignPickerViewModel, kept separate rather than generalizing the Zone one (same "avoid cross-controller coupling" reasoning as EfFarmOpenfieldRepository's own doc comment).
    public class ParcelAssignPickerViewModel
    {
        public int IDFarmParcelZone { get; set; }
        public bool ControllerCapable { get; set; }
        public IList<DeviceDto> Devices { get; set; } = [];
    }
}
