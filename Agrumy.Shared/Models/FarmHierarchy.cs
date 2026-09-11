namespace Agrumy.Shared.Models
{
    /// Chosen once at Farm creation, routes it to exactly one of the two parallel mid/leaf-level branches below.
    public enum FarmType
    {
        Greenhouse = 1,
        OpenField = 2,
    }

    /// Replaces ExperimentScope/SimulationGroupScope/DashboardAggregationLevel - one scope enum every hierarchy-aware subsystem shares. Farm/Unit/Zone values are numerically unchanged from ExperimentScope/DashboardAggregationLevel on purpose; Sowing/FarmParcelZone (formerly Crop/Parcel) keep their prior numeric values too - restructure R1 renamed the concept, not the wire value.
    public enum HierarchyNodeKind
    {
        Global = 0,
        Farm = 1,
        Unit = 2,
        Zone = 3,
        Sowing = 4,
        FarmParcelZone = 5,
    }

    /// Planned -> Active -> Closed (Detaljni dizajn R, D9) - shared by Sowing and ZonePlanting since both are "one growing cycle occupying one or more zones" with the same lifecycle.
    public enum GrowingCycleStatus
    {
        Planned = 1,
        Active = 2,
        Closed = 3,
    }

    /// Detaljni dizajn R katalog tipova unosa - phase column there decides which EntryType values a dnevnik "Add event" dropdown offers.
    public enum EntryType
    {
        Ploughing = 1,
        Discing = 2,
        Harrowing = 3,
        Subsoiling = 4,
        BaseFertilization = 5,
        SoilAnalysis = 6,
        Liming = 7,
        Sowing = 8,
        Planting = 9,
        Fertilization = 10,
        PlantProtection = 11,
        Irrigation = 12,
        Weeding = 13,
        Pruning = 14,
        Observation = 15,
        Scouting = 16,
        Sampling = 17,
        Pollination = 18,
        Harvest = 19,
        PostHarvestAnalysis = 20,
        Storage = 21,
        Sale = 22,
        Other = 23,
    }

    /// Server-side generic view of a mid-level hierarchy node (Unit) - DeviceFarmUnit implements this explicitly so the interface never affects its own wire-format JSON property names.
    public interface IFarmMidLevelNode
    {
        int? Id { get; }
        int? TenantID { get; }
        int? FarmID { get; }
        string? Name { get; }
        int DisplayOrder { get; }
    }

    /// Server-side generic view of a leaf-level hierarchy node (Zone or FarmParcelZone) - carries the full irrigation/heating/dashboard behavior surface, since a FarmParcelZone mirrors a Zone exactly in shape and behavior, not just in position.
    public interface IFarmLeafLevelNode
    {
        int? Id { get; }
        int? TenantID { get; }
        int MidLevelID { get; }
        string? Name { get; }
        int? WaterPumpMaxRunSeconds { get; }
        int? WaterPumpCooldownSeconds { get; }
        bool SkipWaterPumpWhenRainPredicted { get; }
        double? TankCapacityLiters { get; }
        int? WaterLevelRawEmpty { get; }
        int? WaterLevelRawFull { get; }
        double? WaterPumpMinLevel { get; }
        int? HeatingMaxRunSeconds { get; }
        int? VentilationMaxRunSeconds { get; }
        HeatingFailSafePolicyType? HeatingFailSafePolicy { get; }
        List<DashboardWidget> DashboardWidgets { get; }
    }

    /// The Open-Field type-extension row for a Farm whose FarmType is OpenField - 1:1 with its Farm, same "extension row" pattern the HorticultureCatalog subtypes use. Deleted/DeletedAtUtc cascade from the owning Farm's own Deleted, never set independently (same rule as DeviceFarmUnit's Deleted).
    public class FarmOpenfield
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDFarmOpenfield { get; set; }
        public int? TenantID { get; set; }
        public int FarmID { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    /// Global (TenantID null, Global-admin maintained) or tenant-added cultivar/species catalog a Sowing or ZonePlanting references - Detaljni dizajn R, D12: a tenant sees the union of both, edits only its own rows, same shared-plus-own pattern as the existing Horticulture Catalog.
    public class Crop
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDCrop { get; set; }
        public int? TenantID { get; set; }
        public string? Name { get; set; }
        public int? TypicalCycleDays { get; set; }
        /// Which quality metrics this crop's harvestResult.MetricsJson carries (hectoliter weight, protein %, oil %, Brix, dry matter %...) - free-form per crop, not a fixed column set.
        public string? QualityMetricsJson { get; set; }
    }

    /// Open-Field's container above the unit-of-work level (Detaljni dizajn R, D2) - the physical piece of land (ARKOD id, outer boundary, area). FarmParcelZone, not FarmParcel, is where sowings/devices/rules/dnevnik actually attach.
    public class FarmParcel
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDFarmParcel { get; set; }
        public int? TenantID { get; set; }
        public int FarmOpenfieldID { get; set; }
        public string? FarmParcelName { get; set; }
        /// Outer boundary polygon (S-A, D4) - WGS84 GeoJSON Polygon text, validated/normalized by ParcelGeometryValidator before storage.
        public string? GeometryGeoJson { get; set; }
        public double? AreaHectares { get; set; }
        public double? BboxMinLat { get; set; }
        public double? BboxMinLon { get; set; }
        public double? BboxMaxLat { get; set; }
        public double? BboxMaxLon { get; set; }
        public string? ArkodParcelId { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    /// Open-Field's leaf-level, unit-of-work node (Detaljni dizajn R, D2/D3) - a device, its rules, and its dnevnik all attach here, never to FarmParcel directly. Every FarmParcel gets one FarmParcelZone(IsWholeParcel=true) on creation; splitting replaces that single zone with N.
    public class FarmParcelZone : IFarmLeafLevelNode
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDFarmParcelZone { get; set; }
        public int? TenantID { get; set; }
        public int FarmParcelID { get; set; }
        public string? FarmParcelZoneName { get; set; }
        public bool IsWholeParcel { get; set; }
        // D4 - set for as long as a sowing holds this zone; DeviceConfigBuilder/RuleHierarchyResolver read it to build the Sowing tier, split/merge is blocked while it's set.
        public int? CurrentSowingID { get; set; }

        public int? WaterPumpMaxRunSeconds { get; set; }
        public int? WaterPumpCooldownSeconds { get; set; }
        public bool SkipWaterPumpWhenRainPredicted { get; set; }
        public double? TankCapacityLiters { get; set; }
        public int? WaterLevelRawEmpty { get; set; }
        public int? WaterLevelRawFull { get; set; }
        public double? WaterPumpMinLevel { get; set; }
        public int? HeatingMaxRunSeconds { get; set; }
        public int? VentilationMaxRunSeconds { get; set; }
        public HeatingFailSafePolicyType? HeatingFailSafePolicy { get; set; }
        public List<DashboardWidget> DashboardWidgets { get; set; } = [];

        /// Subdivision polygon within the parcel's outer boundary (S-A, D4) - same validation/storage shape as FarmParcel.GeometryGeoJson.
        public string? GeometryGeoJson { get; set; }
        public double? AreaHectares { get; set; }
        public double? BboxMinLat { get; set; }
        public double? BboxMinLon { get; set; }
        public double? BboxMaxLat { get; set; }
        public double? BboxMaxLon { get; set; }

        public DateTimeOffset? DeletedAtUtc { get; set; }

        int? IFarmLeafLevelNode.Id => IDFarmParcelZone;
        int IFarmLeafLevelNode.MidLevelID => FarmParcelID;
        string? IFarmLeafLevelNode.Name => FarmParcelZoneName;
    }

    /// One growing cycle for one crop, possibly spanning several FarmParcelZones on the same farm (D11) - Detaljni dizajn R's replacement for the old "Crop" hierarchy node. Rule scope's Sowing tier (D5) is this, not FarmParcel.
    public class Sowing
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDSowing { get; set; }
        public int? TenantID { get; set; }
        public int FarmID { get; set; }
        public int CropID { get; set; }
        public string? Variety { get; set; }
        public double? SeedRateKgPerHa { get; set; }
        public DateOnly StartDate { get; set; }
        public int ExpectedDurationDays { get; set; }
        public GrowingCycleStatus Status { get; set; } = GrowingCycleStatus.Planned;
        public DateOnly? HarvestDate { get; set; }
        public DateTimeOffset? ClosedUtc { get; set; }
        public int? ClosedByUserID { get; set; }
        public string? Notes { get; set; }
        /// Not a stored column - the repository fills it in from Crop.Name (+ Variety) on every read, since a Sowing has no free-text name of its own; a display convenience for scope pickers/audit labels only.
        public string? SowingName { get; set; }
    }

    /// Which FarmParcelZones a Sowing currently occupies, and its full occupancy history - at most one row per FarmParcelZoneID has ReleasedUtc == null (Detaljni dizajn R's "ActiveKey" invariant, same computed-column-uniqueness pattern used elsewhere in this schema).
    public class SowingFarmParcelZone
    {
        public int SowingID { get; set; }
        public int FarmParcelZoneID { get; set; }
        public DateTimeOffset AssignedUtc { get; set; }
        public DateTimeOffset? ReleasedUtc { get; set; }
    }

    /// Greenhouse's equivalent of Sowing (Detaljni dizajn R, D8) - one planting cycle for one DeviceFarmUnitZone, same Planned/Active/Closed lifecycle, but 1:1 with its zone (no cross-zone spanning the way Sowing allows via D11).
    public class ZonePlanting
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDZonePlanting { get; set; }
        public int? TenantID { get; set; }
        public int DeviceFarmUnitZoneID { get; set; }
        public int CropID { get; set; }
        public DateOnly PlantedDate { get; set; }
        public int ExpectedDurationDays { get; set; }
        public GrowingCycleStatus Status { get; set; } = GrowingCycleStatus.Planned;
        public DateOnly? HarvestDate { get; set; }
        public DateTimeOffset? ClosedUtc { get; set; }
        public string? Notes { get; set; }
        /// Not a stored column, same convenience as Sowing.SowingName - the repository fills it in from Crop.Name on every read.
        public string? CropName { get; set; }
    }

    /// One dnevnik entry, scoped to exactly one of the four FKs below (Detaljni dizajn R, D6/D7) - shared shape for both Open-Field (Sowing/FarmParcelZone) and Greenhouse (ZonePlanting/DeviceFarmUnitZone) so the same "Add event" UI and evidencija/N-bilanca reports work for both worlds.
    public class FieldLogEntry
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDFieldLogEntry { get; set; }
        public int? TenantID { get; set; }
        public int? SowingID { get; set; }
        public int? FarmParcelZoneID { get; set; }
        public int? ZonePlantingID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }
        public EntryType EntryType { get; set; }
        public DateTimeOffset DateUtc { get; set; }
        public int? CreatedByUserID { get; set; }
        public string? Note { get; set; }
        /// Per-EntryType structured fields (PlantProtection dose/PHI, Fertilization N-P-K, SoilAnalysis pH/humus...) - see Detaljni dizajn R's katalog tipova unosa table for the shape per type.
        public string? PayloadJson { get; set; }
        /// True for the Harvest entry that closes a Sowing/ZonePlanting (D9) - drives the Close-sowing/Close-planting transition, not a separate flag the caller sets independently.
        public bool IsClosingEntry { get; set; }
    }

    public class FieldLogAttachment
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDFieldLogAttachment { get; set; }
        public int FieldLogEntryID { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public string? StoragePath { get; set; }
        public long SizeBytes { get; set; }
    }

    /// Closing-time yield record for a Sowing or ZonePlanting (D14) - FarmParcelZoneID null means a grouped result for the whole sowing; a per-zone breakdown is optional, the grouped row stays the default/required one at close time.
    public class HarvestResult
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDHarvestResult { get; set; }
        public int? SowingID { get; set; }
        public int? ZonePlantingID { get; set; }
        public int? FarmParcelZoneID { get; set; }
        public DateTimeOffset DateUtc { get; set; }
        public double YieldKg { get; set; }
        public double? MoisturePercent { get; set; }
        public string? QualityGrade { get; set; }
        public double? LossesKg { get; set; }
        /// Per-crop metrics named in that Crop.QualityMetricsJson - free-form, not a fixed column set.
        public string? MetricsJson { get; set; }
        public string? Note { get; set; }
    }

    /// Body of the Open-Field Add Controller/Add Sensor action - same shape as DeviceZoneAssignment, assigns an unassigned device to a FarmParcelZone.
    public class DeviceParcelAssignment
    {
        public int IDDevice { get; set; }
        public int IDFarmParcelZone { get; set; }
    }

    /// Body of the FarmParcelZone merge action (D3/D4) - merges every listed zone (must all be free) back into one.
    public class ParcelMergeRequest
    {
        public List<int> FarmParcelZoneIds { get; set; } = [];
        public string MergedName { get; set; } = "";
    }

    /// Body of the sjetva wizard's "Finish" step (D3/D9/D11) - occupies every listed (must-be-free) zone and flips the sowing to Active.
    public class SowingStartRequest
    {
        public int IDSowing { get; set; }
        public List<int> FarmParcelZoneIds { get; set; } = [];
    }

    /// Body of the "Close sowing" action (D9/D14) - a grouped harvest result plus the closing dnevnik entry. Confirm must be true when the sowing has an unexpired EarliestHarvestDate (D13) - the API returns 409 with that date otherwise, the same "explicit confirmation" pattern as other safety-gated writes.
    public class SowingCloseRequest
    {
        public int IDSowing { get; set; }
        public double YieldKg { get; set; }
        public double? MoisturePercent { get; set; }
        public string? QualityGrade { get; set; }
        public string? Note { get; set; }
        public bool Confirm { get; set; }
    }

    /// Body of the parcel/zone geometry PUT (S-A) - ArkodParcelId only meaningful on the FarmParcel (container) endpoint, ignored on the zone one.
    public class ParcelGeometrySetRequest
    {
        public string GeometryGeoJson { get; set; } = "";
        public string? ArkodParcelId { get; set; }
    }

    /// Body of the greenhouse "Start planting" action (D8) - crop name is resolved against the same catalog Sowing uses (D12).
    public class ZonePlantingStartRequest
    {
        public string CropName { get; set; } = "";
        public DateOnly PlantedDate { get; set; }
        public int ExpectedDurationDays { get; set; }
    }

    /// Body of the greenhouse "Close planting" action (D8/D13) - same karenca-confirm gate as SowingCloseRequest.
    public class ZonePlantingCloseRequest
    {
        public double YieldKg { get; set; }
        public double? MoisturePercent { get; set; }
        public string? QualityGrade { get; set; }
        public string? Note { get; set; }
        public bool Confirm { get; set; }
    }

    /// FieldLogEntry.PayloadJson shape for Fertilization/BaseFertilization - Detaljni dizajn R's katalog tipova unosa. N/P/K percentages are of the product, not of the dose.
    public class FertilizationPayload
    {
        public string? Product { get; set; }
        public double? NPercent { get; set; }
        public double? PPercent { get; set; }
        public double? KPercent { get; set; }
        public double DoseKgPerHa { get; set; }
        public double AreaHa { get; set; }
    }

    /// FieldLogEntry.PayloadJson shape for SoilAnalysis.
    public class SoilAnalysisPayload
    {
        public double? PH { get; set; }
        public double? HumusPercent { get; set; }
        public double? P2O5 { get; set; }
        public double? K2O { get; set; }
        public double? NMin { get; set; }
        public double? DepthCm { get; set; }
        public string? Laboratory { get; set; }
    }

    /// FieldLogEntry.PayloadJson shape for PlantProtection - every field here is legally required (D7, Pravilnik o održivoj uporabi pesticida). PhiDays (karenca) drives Sowing.EarliestHarvestDate (D13).
    public class PlantProtectionPayload
    {
        public string ProductName { get; set; } = "";
        public string ActiveSubstance { get; set; } = "";
        public string Dose { get; set; } = "";
        public double TreatedAreaHa { get; set; }
        public string Reason { get; set; } = "";
        public int PhiDays { get; set; }
        public string Applicator { get; set; } = "";
        public string? WeatherConditions { get; set; }
    }

    /// FieldLogEntry.PayloadJson shape for Irrigation - exactly one of AmountMm/AmountM3 is meaningful, matching whichever unit the farm measures in.
    public class IrrigationPayload
    {
        public double? AmountMm { get; set; }
        public double? AmountM3 { get; set; }
        public int? DurationMinutes { get; set; }
        public string? Source { get; set; }
    }

    /// Rehomed onto Sowing by restructure R (Detaljni dizajn R, sesija 2) - same sensor-average/status/trend shape as DeviceFarmUnitDashboard so the Farms page renders both branches with identical styling.
    public class SowingDashboard
    {
        public int IDSowing { get; set; }
        public string? SowingName { get; set; }
        public int FarmID { get; set; }
        public int ZoneCount { get; set; }
        public int DeviceCount { get; set; }
        public SensorAverages Averages { get; set; } = new();
        public ZoneStatus Status { get; set; }
        public SensorTrend Trend { get; set; } = new();
        public IList<UnitZoneProblemAlert> ProblemAlerts { get; set; } = new List<UnitZoneProblemAlert>();
    }

    /// Rehomed onto FarmParcelZone by restructure R - same shape as DeviceFarmUnitZoneDashboard narrowed to one zone.
    public class FarmParcelZoneDashboard
    {
        public int IDFarmParcelZone { get; set; }
        public int? IDSowing { get; set; }
        public string? FarmParcelZoneName { get; set; }
        public int DeviceCount { get; set; }
        public SensorAverages Averages { get; set; } = new();
        public ZoneStatus Status { get; set; }
        public SensorTrend Trend { get; set; } = new();
        public IList<UnitZoneProblemAlert> ProblemAlerts { get; set; } = new List<UnitZoneProblemAlert>();
    }
}
