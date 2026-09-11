using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// One FarmParcel plus every zone it currently has, each already knowing whether it's free or which Sowing holds it - drives the per-parcel card on FarmOpenfield/Index.cshtml.
    public class FarmParcelWithZonesViewModel
    {
        public required FarmParcel Parcel { get; init; }
        public IList<FarmParcelZone> Zones { get; init; } = [];
    }

    /// One Open-Field farm's full picture - its parcels+zones and its sowings - drives one farm card on FarmOpenfield/Index.cshtml (D1/D2).
    public class FarmOpenfieldFarmViewModel
    {
        public required DeviceFarm Farm { get; init; }
        public FarmOpenfield? Openfield { get; init; }
        public IList<FarmParcelWithZonesViewModel> Parcels { get; init; } = [];
        public IList<Sowing> Sowings { get; init; } = [];
    }

    /// Drives FarmOpenfield/Index.cshtml - the dedicated Open-Field root page (D1), replacing the old mixed Farms page's crop-cube section for everything this session builds.
    public class FarmOpenfieldIndexViewModel
    {
        public IList<FarmOpenfieldFarmViewModel> Farms { get; init; } = [];
    }

    /// Drives FarmOpenfield/Parcels.cshtml - one Sowing's own detail/lifecycle page (Sowing Details, D9), the Open-Field mirror of UnitZonesViewModel.
    public class CropParcelsViewModel
    {
        public required Sowing Crop { get; init; }
        public required DeviceFarm Farm { get; init; }
        public IList<FarmParcelZoneDashboard> Parcels { get; init; } = [];
        /// Every zone on the sowing's own farm, free or not - the Start-sowing zone picker (D3/D9) when the sowing is still Planned. Empty once Active/Closed.
        public IList<FarmParcelWithZonesViewModel> AvailableParcels { get; init; } = [];
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
