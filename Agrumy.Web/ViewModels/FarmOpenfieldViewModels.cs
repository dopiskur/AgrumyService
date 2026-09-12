using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// One FarmParcel plus every zone it currently has, each already knowing whether it's free or which Sowing holds it - drives the per-parcel card on FarmOpenfield/Index.cshtml.
    public class FarmParcelWithZonesViewModel
    {
        public required FarmParcel Parcel { get; init; }
        public IList<FarmParcelZone> Zones { get; init; } = [];
    }

    /// Drives FarmOpenfield/Index.cshtml - the Open-Field farm register only (name/satellite/add); parcels and sowings moved to their own pages (Parcels registry, Crop Seasons) so this block no longer mixes all three.
    public class FarmOpenfieldIndexViewModel
    {
        public IList<DeviceFarm> Farms { get; init; } = [];
    }

    /// One row on FarmOpenfield/ParcelsRegistry.cshtml - a parcel (or one of its split zones) plus which farm it belongs to, since the registry flattens every farm into one list.
    public class ParcelRegistryRowViewModel
    {
        public required string FarmName { get; init; }
        public required FarmParcel Parcel { get; init; }
        public required FarmParcelZone Zone { get; init; }
    }

    /// One Open-Field farm's own Openfield extension id - the "Add Parcel" form's farm picker on ParcelsRegistry.cshtml needs FarmParcelAdd's idFarmOpenfield, not the farm's own id.
    public class ParcelRegistryFarmOptionViewModel
    {
        public required string FarmName { get; init; }
        public required int IdFarmOpenfield { get; init; }
    }

    /// Drives FarmOpenfield/ParcelsRegistry.cshtml - the Fleet-style overview of every parcel/zone across every Open-Field farm, separated from the farm register (Index) and Crop Seasons.
    public class ParcelsRegistryViewModel
    {
        public IList<ParcelRegistryRowViewModel> Rows { get; init; } = [];
        public IList<ParcelRegistryFarmOptionViewModel> Farms { get; init; } = [];
    }

    /// Drives FarmOpenfield/CropSeasons.cshtml - every sowing across every Open-Field farm, plus what "New sowing" needs to build one (farm picker, catalog-driven crop/variety picker).
    public class CropSeasonsIndexViewModel
    {
        public IList<DeviceFarm> Farms { get; init; } = [];
        public IList<Sowing> Sowings { get; init; } = [];
        /// HorticultureCatalogType.Crop entries (wheat/corn + variety, BBCH-staged) - "New sowing" picks a Name from here instead of typing free text; Sowing.CropID still resolves through the separate lightweight Crop catalog by that same name (ICropCatalogRepository.CropFindOrCreateByNameAsync), no new FK.
        public IList<HorticultureCatalogEntry> CatalogCrops { get; init; } = [];
    }

    /// Drives FarmOpenfield/Parcels.cshtml - one Sowing's own detail/lifecycle page (Sowing Details, D9), the Open-Field mirror of UnitZonesViewModel.
    public class CropParcelsViewModel
    {
        public required Sowing Crop { get; init; }
        public required DeviceFarm Farm { get; init; }
        public IList<FarmParcelZoneDashboard> Parcels { get; init; } = [];
        /// Every zone on the sowing's own farm, free or not - the Start-sowing zone picker (D3/D9) when the sowing is still Planned. Empty once Active/Closed.
        public IList<FarmParcelWithZonesViewModel> AvailableParcels { get; init; } = [];
        public IList<FieldLogEntry> LogEntries { get; init; } = [];
        /// D13 - null means no PlantProtection entry has been logged yet, so Harvest is never karenca-gated.
        public DateOnly? EarliestHarvestDate { get; init; }
        /// "Bilanca N" - kg N per ha across every fertilization entry so far; null until the first one.
        public double? NitrogenBalanceKgPerHa { get; init; }
    }

    /// Drives the FieldLog "Add event" form on FarmOpenfield/Parcels.cshtml - one flattened input covering every EntryType's own fields; the controller picks which sub-set actually matters based on EntryType and assembles the right PayloadJson server-side.
    public class FieldLogEntryFormInput
    {
        public EntryType EntryType { get; set; }
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public string? Note { get; set; }

        // Fertilization / BaseFertilization
        public string? Product { get; set; }
        public double? NPercent { get; set; }
        public double? PPercent { get; set; }
        public double? KPercent { get; set; }
        public double? DoseKgPerHa { get; set; }
        public double? AreaHa { get; set; }

        // SoilAnalysis
        public double? PH { get; set; }
        public double? HumusPercent { get; set; }
        public double? P2O5 { get; set; }
        public double? K2O { get; set; }
        public double? NMin { get; set; }
        public double? DepthCm { get; set; }
        public string? Laboratory { get; set; }

        // PlantProtection (all legally required when EntryType is PlantProtection)
        public string? ProductName { get; set; }
        public string? ActiveSubstance { get; set; }
        public string? Dose { get; set; }
        public double? TreatedAreaHa { get; set; }
        public string? Reason { get; set; }
        public int? PhiDays { get; set; }
        public string? Applicator { get; set; }
        public string? WeatherConditions { get; set; }

        // Irrigation
        public double? AmountMm { get; set; }
        public double? AmountM3 { get; set; }
        public int? DurationMinutes { get; set; }
        public string? Source { get; set; }
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
