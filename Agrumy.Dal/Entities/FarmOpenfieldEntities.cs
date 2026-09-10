namespace Agrumy.Dal.Entities
{
    /// 1:1 type-extension row for a Farm whose FarmType is OpenField - see Agrumy.Shared.Models.FarmOpenfield.
    public class FarmOpenfieldRow
    {
        public int IDFarmOpenfield { get; set; }
        public int? TenantID { get; set; }
        public int FarmID { get; set; }
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    public class FarmOpenfieldCropRow
    {
        public int IDFarmOpenfieldCrop { get; set; }
        public int? TenantID { get; set; }
        public string? FarmOpenfieldCropName { get; set; }
        public int FarmOpenfieldID { get; set; }
        public int DisplayOrder { get; set; }
        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    /// See Agrumy.Shared.Models.FarmOpenfieldCropParcel - same behavior-column set as DeviceFarmUnitZoneRow, TenantID denormalized from FarmOpenfieldCrop the same way.
    public class FarmOpenfieldCropParcelRow
    {
        public int IDFarmOpenfieldCropParcel { get; set; }
        public int? TenantID { get; set; }
        public int FarmOpenfieldCropID { get; set; }
        public string? FarmOpenfieldCropParcelName { get; set; }

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

        public bool Deleted { get; set; }
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }
}
