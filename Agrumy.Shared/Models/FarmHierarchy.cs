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
}
