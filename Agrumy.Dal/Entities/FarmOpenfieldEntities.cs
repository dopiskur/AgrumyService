namespace Agrumy.Dal.Entities
{
    /// See Agrumy.Shared.Models.Crop - TenantID null means the global, Global-admin-maintained catalog row (D12).
    public class CropRow
    {
        public int IDCrop { get; set; }
        public int? TenantID { get; set; }
        public string? Name { get; set; }
        public int? TypicalCycleDays { get; set; }
        public string? QualityMetricsJson { get; set; }
    }

    /// See Agrumy.Shared.Models.FarmParcel - the container above FarmParcelZone (D2); no behavior columns live here, those are on the zone.
    public class FarmParcelRow
    {
        public int IDFarmParcel { get; set; }
        public int? TenantID { get; set; }
        public int FarmID { get; set; }
        public string? FarmParcelName { get; set; }
        public string? GeometryGeoJson { get; set; }
        public double? AreaHectares { get; set; }
        public double? BboxMinLat { get; set; }
        public double? BboxMinLon { get; set; }
        public double? BboxMaxLat { get; set; }
        public double? BboxMaxLon { get; set; }
        public string? ArkodParcelId { get; set; }
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    /// See Agrumy.Shared.Models.FarmParcelZone - same behavior-column set as DeviceFarmUnitZoneRow, TenantID denormalized from FarmParcel the same way.
    public class FarmParcelZoneRow
    {
        public int IDFarmParcelZone { get; set; }
        public int? TenantID { get; set; }
        public int FarmParcelID { get; set; }
        public string? FarmParcelZoneName { get; set; }
        public bool IsWholeParcel { get; set; }
        public int? CurrentSowingID { get; set; }
        // Resets to false whenever SowingStartAsync occupies this zone with a new sowing - pre-season field prep (ploughing/discing/fertilizing) has to be redone/re-confirmed each season.
        public bool ReadyForSeason { get; set; }

        public int? WaterPumpMaxRunSeconds { get; set; }
        public int? WaterPumpCooldownSeconds { get; set; }
        public bool SkipWaterPumpWhenRainPredicted { get; set; }
        public double? TankCapacityLiters { get; set; }
        public int? WaterLevelRawEmpty { get; set; }
        public int? WaterLevelRawFull { get; set; }
        public DateTimeOffset? TankRefillNotifiedAt { get; set; }
        public double? WaterPumpMinLevel { get; set; }
        public int? HeatingMaxRunSeconds { get; set; }
        public int? VentilationMaxRunSeconds { get; set; }
        public int? HeatingFailSafePolicy { get; set; }
        public string? DashboardWidgetsJson { get; set; }
        public int DashboardGridColumns { get; set; } = 4;

        public string? GeometryGeoJson { get; set; }
        public double? AreaHectares { get; set; }
        public double? BboxMinLat { get; set; }
        public double? BboxMinLon { get; set; }
        public double? BboxMaxLat { get; set; }
        public double? BboxMaxLon { get; set; }
        public DateTimeOffset? SatelliteBackfillCompletedUtc { get; set; }

        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    /// See Agrumy.Shared.Models.Sowing - restructure R's replacement mid-level rule scope for the Open-Field branch (D5), migrated from the old farmOpenfieldCrop table.
    public class SowingRow
    {
        public int IDSowing { get; set; }
        public int? TenantID { get; set; }
        public int FarmID { get; set; }
        public int CropID { get; set; }
        public string? Variety { get; set; }
        public double? SeedRateKgPerHa { get; set; }
        public DateOnly StartDate { get; set; }
        public int ExpectedDurationDays { get; set; }
        public int Status { get; set; }
        public DateOnly? HarvestDate { get; set; }
        public DateTimeOffset? ClosedUtc { get; set; }
        public int? ClosedByUserID { get; set; }
        public string? Notes { get; set; }
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    /// See Agrumy.Shared.Models.SowingFarmParcelZone - composite PK (SowingID, FarmParcelZoneID); the ActiveKey computed column below enforces D4's "at most one open occupancy per zone" invariant.
    public class SowingFarmParcelZoneRow
    {
        public int SowingID { get; set; }
        public int FarmParcelZoneID { get; set; }
        public DateTimeOffset AssignedUtc { get; set; }
        public DateTimeOffset? ReleasedUtc { get; set; }
    }

    /// See Agrumy.Shared.Models.FarmParcelGroupCrop - always scoped to one Farm, never mixes parcels from different farms.
    public class FarmParcelGroupCropRow
    {
        public int IDFarmParcelGroupCrop { get; set; }
        public int? TenantID { get; set; }
        public int FarmID { get; set; }
        public string? Name { get; set; }
    }

    /// Static membership only (no assigned/released timestamps, unlike SowingFarmParcelZoneRow) - composite PK (FarmParcelGroupCropID, FarmParcelID).
    public class FarmParcelGroupCropMemberRow
    {
        public int FarmParcelGroupCropID { get; set; }
        public int FarmParcelID { get; set; }
    }

    /// See Agrumy.Shared.Models.ZonePlanting - Greenhouse's equivalent of Sowing (D8), 1:1 with its DeviceFarmUnitZone.
    public class ZonePlantingRow
    {
        public int IDZonePlanting { get; set; }
        public int? TenantID { get; set; }
        public int DeviceFarmUnitZoneID { get; set; }
        public int CropID { get; set; }
        public DateOnly PlantedDate { get; set; }
        public int ExpectedDurationDays { get; set; }
        public int Status { get; set; }
        public DateOnly? HarvestDate { get; set; }
        public DateTimeOffset? ClosedUtc { get; set; }
        public string? Notes { get; set; }
    }

    /// See Agrumy.Shared.Models.FieldLogEntry - exactly one of the four scope FKs is set, enforced in the API layer (same pattern as DeviceFarmUnitZoneRuleRow's six scope FKs).
    public class FieldLogEntryRow
    {
        public int IDFieldLogEntry { get; set; }
        public int? TenantID { get; set; }
        public int? SowingID { get; set; }
        public int? FarmParcelZoneID { get; set; }
        public int? ZonePlantingID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }
        public int EntryType { get; set; }
        public DateTimeOffset DateUtc { get; set; }
        public int? CreatedByUserID { get; set; }
        public string? Note { get; set; }
        public string? PayloadJson { get; set; }
        public bool IsClosingEntry { get; set; }
    }

    public class FieldLogAttachmentRow
    {
        public int IDFieldLogAttachment { get; set; }
        public int FieldLogEntryID { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public string? StoragePath { get; set; }
        public long SizeBytes { get; set; }
    }

    /// See Agrumy.Shared.Models.HarvestResult - FarmParcelZoneID null means a grouped result for the whole Sowing/ZonePlanting (D14).
    public class HarvestResultRow
    {
        public int IDHarvestResult { get; set; }
        public int? SowingID { get; set; }
        public int? ZonePlantingID { get; set; }
        public int? FarmParcelZoneID { get; set; }
        public DateTimeOffset DateUtc { get; set; }
        public double YieldKg { get; set; }
        public double? MoisturePercent { get; set; }
        public string? QualityGrade { get; set; }
        public double? LossesKg { get; set; }
        public string? MetricsJson { get; set; }
        public string? Note { get; set; }
    }
}
