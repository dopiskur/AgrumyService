namespace Agrumy.Shared.Models
{
    /// Chosen once at Farm creation, routes it to exactly one of the two parallel mid/leaf-level branches below.
    public enum FarmType
    {
        Greenhouse = 1,
        OpenField = 2,
    }

    /// Replaces ExperimentScope/SimulationGroupScope/DashboardAggregationLevel - one scope enum every hierarchy-aware subsystem shares. Farm/Unit/Zone values are numerically unchanged from ExperimentScope/DashboardAggregationLevel on purpose, so neither needed a data migration; Crop/Parcel are purely additive.
    public enum HierarchyNodeKind
    {
        Global = 0,
        Farm = 1,
        Unit = 2,
        Zone = 3,
        Crop = 4,
        Parcel = 5,
    }

    /// Server-side generic view of a mid-level hierarchy node (Unit or Crop) - DeviceFarmUnit/FarmOpenfieldCrop implement this explicitly so the interface never affects their own wire-format JSON property names.
    public interface IFarmMidLevelNode
    {
        int? Id { get; }
        int? TenantID { get; }
        int? FarmID { get; }
        string? Name { get; }
        int DisplayOrder { get; }
    }

    /// Server-side generic view of a leaf-level hierarchy node (Zone or Parcel) - carries the full irrigation/heating/dashboard behavior surface, since a Parcel mirrors a Zone exactly in shape and behavior, not just in position.
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

    /// Open-Field's mid-level node, mirrors DeviceFarmUnit exactly - "a growing crop within one FarmOpenfield containing FarmOpenfieldCropParcels".
    public class FarmOpenfieldCrop : IFarmMidLevelNode
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDFarmOpenfieldCrop { get; set; }
        public int? TenantID { get; set; }
        public string? FarmOpenfieldCropName { get; set; }
        public int FarmOpenfieldID { get; set; }
        public int DisplayOrder { get; set; }

        int? IFarmMidLevelNode.Id => IDFarmOpenfieldCrop;
        int? IFarmMidLevelNode.FarmID => FarmOpenfieldID;
        string? IFarmMidLevelNode.Name => FarmOpenfieldCropName;
    }

    /// Open-Field's leaf-level node, mirrors DeviceFarmUnitZone exactly - same irrigation/heating/dashboard behavior surface, "one parcel = one controller" at most, may be sensor-only.
    public class FarmOpenfieldCropParcel : IFarmLeafLevelNode
    {
        [Microsoft.AspNetCore.Mvc.HiddenInput(DisplayValue = true)]
        public int? IDFarmOpenfieldCropParcel { get; set; }
        public int? TenantID { get; set; }
        public int FarmOpenfieldCropID { get; set; }
        public string? FarmOpenfieldCropParcelName { get; set; }

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

        int? IFarmLeafLevelNode.Id => IDFarmOpenfieldCropParcel;
        int IFarmLeafLevelNode.MidLevelID => FarmOpenfieldCropID;
        string? IFarmLeafLevelNode.Name => FarmOpenfieldCropParcelName;
    }

    /// Body of the Open-Field Add Controller/Add Sensor action - same shape as DeviceZoneAssignment, assigns an unassigned device to a parcel.
    public class DeviceParcelAssignment
    {
        public int IDDevice { get; set; }
        public int IDFarmOpenfieldCropParcel { get; set; }
    }
}
