using Microsoft.AspNetCore.Mvc;

namespace api.Models
{
    /// Roadmap #384 - top-level organizational grouping ABOVE Unit within the same tenant (a physical farm/site, e.g. separate LoRa networks or gateways naturally map to separate Farms); optional, a DeviceFarmUnit not yet assigned to one has DeviceFarmID null.
    public class DeviceFarm
    {
        [HiddenInput(DisplayValue = true)]
        public int? IDDeviceFarm { get; set; }
        public int? TenantID { get; set; }
        public string? DeviceFarmName { get; set; }
        // Roadmap #409 - null unless this came from the Recycle Bin listing.
        public DateTimeOffset? DeletedAtUtc { get; set; }
    }

    /// A physical/logical space (e.g. a greenhouse) containing DeviceFarmUnitZones; TenantID null only means the shared IDDeviceFarmUnit=0 "Default" sentinel every unzoned device points at.
    public class DeviceFarmUnit
    {
        [HiddenInput(DisplayValue = true)]
        public int? IDDeviceFarmUnit { get; set; }
        public int? TenantID { get; set; }
        public string? DeviceFarmUnitName { get; set; }
        // Roadmap #384 - optional (a Farm-less Unit stays valid, no default-farm backfill).
        public int? DeviceFarmID { get; set; }
    }

    /// A growing zone within one DeviceFarmUnit - "one zone = one controller" at most, may be sensor-only; TenantID is denormalized from DeviceFarmUnit so a zone query needs no join to check ownership.
    public class DeviceFarmUnitZone
    {
        [HiddenInput(DisplayValue = true)]
        public int? IDDeviceFarmUnitZone { get; set; }
        public int? TenantID { get; set; }
        public int DeviceFarmUnitID { get; set; }
        public string? DeviceFarmUnitZoneName { get; set; }

        // WaterPump-only hard safety ceiling, not a Rule - applied by the device after a rule already decided WaterPump should run; seeded from AgrumySettings on creation.
        public int? WaterPumpMaxRunSeconds { get; set; }
        public int? WaterPumpCooldownSeconds { get; set; }

        // Per-zone opt-in, not a global switch; combined server-side with ServerConfig.WeatherRainPredicted into DeviceConfigController.SkipWaterPumpForRain.
        public bool SkipWaterPumpWhenRainPredicted { get; set; }

        // Tank calibration (roadmap #234) - all three null means "no tank tracking for this zone", not a zero-capacity tank. TankFillPercent/TankVolumeLiters (api.Utils.TankCalculator) are derived from these plus the zone's latest WaterLevel, never stored.
        public double? TankCapacityLiters { get; set; }
        /// Raw sensorData.WaterLevel reading when the tank is empty - not necessarily 0, depends on the physical sensor.
        public int? WaterLevelRawEmpty { get; set; }
        /// Raw sensorData.WaterLevel reading when the tank is full.
        public int? WaterLevelRawFull { get; set; }

        // Dry-run protection - blocks WaterPump (device-side, covers Interval/Schedule/Manual too, not just Threshold) below this fill percent. Null/<=0, or WaterLevelRawEmpty==WaterLevelRawFull (no tank calibration), disables it - a Water Valve zone with no tank sensor to protect.
        public double? WaterPumpMinLevel { get; set; }

        // Roadmap #219 - generalizes WaterPumpMaxRunSeconds above to the other two manually-triggerable functions; only ever used to compute a manual command's hard ExpiresAtUtc cap (api.Commands.ManualActuateService), not applied to automated rule-driven runs the way WaterPump's own cap is.
        public int? HeatingMaxRunSeconds { get; set; }
        public int? VentilationMaxRunSeconds { get; set; }

        // Roadmap #238 - admin-arranged dashboard widgets for this zone's own detail page, in display order. Never null (empty list means "show the default layout only") - see EfDeviceFarmUnitRepository's (de)serialization, same JSON-blob-at-the-app-layer convention as DeviceFarmUnitZoneRule.RootConditionJson. Stored server-side (not per-viewer) so a future mobile client renders the exact same layout, same reasoning the roadmap gave for this design.
        public List<DashboardWidget> DashboardWidgets { get; set; } = [];
    }

    public enum DashboardWidgetType
    {
        SensorValue = 1,
        SensorTrend = 2,
        RelayStatus = 3,
        Text = 4,
    }

    /// One tile on a Zone's customizable dashboard (roadmap #238) - only the fields matching Type are meaningful (flat, tagged-union style, same convention as AgrumyFirmware's wire structs). Label is required for Text, optional elsewhere (overrides the auto-generated title, e.g. "Metric" -> its own name).
    public class DashboardWidget
    {
        public DashboardWidgetType Type { get; set; }
        public SensorMetric? Metric { get; set; }
        public RelayFunction? RelayFunction { get; set; }
        public string? Label { get; set; }
    }

    /// Relay function a DeviceFarmUnitZoneRule targets, same numeric convention as deviceTypeRelay seed rows; kept as a plain int on the wire (not this enum) so firmware can parse it as a number without JsonStringEnumConverter.
    public enum RelayFunction
    {
        Ventilation = 1,
        Light = 2,
        Heating = 3,
        WaterPump = 4,
    }

    /// One entry in POST /api/ControllerData's array - sent every time a relay's on/off state actually CHANGES, not on a fixed interval like SensorData; a real device pushes this alongside a physical relay flip, a simulated one alongside its calculated equivalent, same wire shape either way.
    public class ControllerDataPush
    {
        public RelayFunction RelayFunction { get; set; }
        public bool IsOn { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }

    /// Current on/off state for one RelayFunction on one device - GET /api/ControllerData's shape, and what DeviceFleetStatus.RelayStates carries.
    public class ControllerDataStatus
    {
        public RelayFunction RelayFunction { get; set; }
        public bool IsOn { get; set; }
        public DateTimeOffset? DateChanged { get; set; }
    }

    /// Which measured quantity one ComparisonNode reads - RAW (1-13, a real sensor reading) or DERIVED (14+, computed on-the-fly from raw readings, never a stored column). Explicit per-condition (roadmap #396(4)) - the old model forced every condition in a rule to read the same metric the rule's own RelayFunction implied, making "temp>30 AND humidity<40" structurally inexpressible.
    public enum SensorMetric
    {
        Temperature = 1,
        SoilTemperature = 2,
        Humidity = 3,
        Vpd = 4,
        Moisture = 5,
        Light = 6,
        Co2 = 7,
        Tvoc = 8,
        Barometer = 9,
        LiquidPH = 10,
        RainLevel = 11,
        WaterLevel = 12,
        Wind = 13,
        /// DERIVED (api.Utils.DewPointCalculator, Magnus formula) - Temperature+Humidity.
        DewPoint = 14,
        /// DERIVED - Temperature minus DewPoint; a small/shrinking spread is an early condensation/fungal-disease signal, distinct from absolute humidity alone.
        DewPointSpread = 15,
    }

    /// What a rule does once its Conditions fold to true - Relay is evaluated on-device (AgrumyFirmware's ActuatorController), Notification is evaluated server-side (api.BackgroundWorkers.RuleNotificationEvaluator) since firmware has no notification capability.
    public enum ActionType
    {
        Relay = 1,
        Notification = 2,
    }

    /// AND/OR between two consecutive Conditions - folded strictly left-to-right ("(A AND B) OR C", never "A AND (B OR C)"), no parentheses/nesting.
    public enum LogicalOperator
    {
        And = 1,
        Or = 2,
    }

    /// Which kind of leaf/branch one ConditionNode is - only the fields matching Type are meaningful (flat tagged-union, same wire convention AgrumyFirmware's old Condition struct used, just now recursive via GroupNode.Children).
    public enum NodeType
    {
        Comparison = 1,
        Interval = 2,
        Schedule = 3,
        Group = 4,
        /// Never reaches evaluation as-is on either action path - api.Devices.AstronomicalRuleResolver compiles every occurrence (anywhere in the tree) into an effective Schedule node for today's local date first (Relay: before the device config is sent; Notification: roadmap #398(2), resolved server-side each tick).
        Astronomical = 5,
        /// Only valid inside a Notification-action rule - a Relay-action rule fires invisibly on-device, so the server has no way to observe it as a trigger.
        RuleTriggered = 6,
        /// Roadmap #398(1) - only valid inside a Notification-action rule; compares a live reading against api.Models.SensorTrend's hourly history, which only the server (not firmware) has.
        RateOfChange = 7,
        /// Roadmap #398(3) - only valid inside a Notification-action rule, same SensorTrend dependency as RateOfChange; always reads Temperature, no Metric field.
        DifDisruption = 8,
    }

    /// GT/LT mirror the old Threshold condition's dead-zone latch (Hysteresis); GTE/LTE/Equal/Between are plain stateless comparisons with no latch - they're new, and a dead zone doesn't generalize cleanly to "equals" or "between" anyway.
    public enum ComparisonOperator
    {
        GreaterThan = 1,
        LessThan = 2,
        GreaterThanOrEqual = 3,
        LessThanOrEqual = 4,
        Equal = 5,
        /// Inclusive both ends: Value1 &lt;= reading &lt;= Value2.
        Between = 6,
    }

    /// Recursive rule-condition tree (roadmap #396(4)) - replaces the old flat Conditions[]+left-to-right-fold entirely (alfa phase, no backward compat). GroupNode.Children recurse arbitrarily, enabling real grouping ("(A AND B) OR (C AND D)"); every other Type is a leaf. A ComparisonNode's Metric is explicit and independent per condition - the old model forced every condition in a Relay rule to read the same metric its RelayFunction implied.
    public class ConditionNode
    {
        public NodeType Type { get; set; }

        // Comparison only.
        public SensorMetric? Metric { get; set; }
        public ComparisonOperator? Operator { get; set; }
        public double? Value1 { get; set; }
        /// Between only.
        public double? Value2 { get; set; }
        public double? Hysteresis { get; set; }

        // Interval only.
        public int? Interval { get; set; }
        public int? IntervalLength { get; set; }

        // Schedule: DaysOfWeek+Start+Duration. Astronomical: DaysOfWeek+the two offsets. 7-bit DaysOfWeek mask, bit0=Sunday.
        public int? DaysOfWeek { get; set; }
        public int? Start { get; set; }
        public int? Duration { get; set; }
        /// Astronomical only - negative extends the window earlier than sunrise/sunset, positive later.
        public int? SunriseOffsetMinutes { get; set; }
        public int? SunsetOffsetMinutes { get; set; }

        /// RuleTriggered only - another Notification-action rule (same tenant, any zone/unit) whose own tree folded true this tick.
        public int? ReferencedRuleId { get; set; }

        // Group only.
        public LogicalOperator? GroupOperator { get; set; }
        public IList<ConditionNode> Children { get; set; } = [];

        /// RateOfChange only - compares the live reading against the SensorTrend bucket WindowHours ago (1-23); fires when the absolute difference reaches ChangeThreshold.
        public int? WindowHours { get; set; }
        public double? ChangeThreshold { get; set; }

        // DifDisruption only - fires when (day average - night average) drops below MinDifDegrees, i.e. the night didn't cool enough relative to the day before it. Both windows are relative to "now", not calendar/sunrise-aligned; NightWindowHours+DayWindowHours must not exceed SensorTrend.HourBuckets.
        public int? NightWindowHours { get; set; }
        public int? DayWindowHours { get; set; }
        public double? MinDifDegrees { get; set; }
    }

    /// A materialized JsonNode's keys are frozen by whatever options built it - an outer JsonSerializer.Serialize(camelCaseOptions) does NOT re-key it, so every ConditionNode tree read/write must use these exact Options or camelCase drifts to PascalCase.
    public static class ConditionConfigJson
    {
        public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web);
    }

    /// One automation rule at exactly one scope - DeviceFarmUnitZoneID set means Zone scope, DeviceFarmUnitID set means Unit scope, DeviceFarmID set means Farm scope, all three null means Global (per-tenant: every farm/unit/zone the tenant owns). Several rules at the SAME scope for the same RelayFunction still OR together; Notification rules override by Name instead (a more specific scope's rule with the SAME Name replaces a less specific one, different names always coexist) since a rule's conditions can now span several metrics. IsSafetyRule (roadmap #396(5)) rules always survive being overridden regardless of scope - see api.Devices.RuleHierarchyResolver.
    public class DeviceFarmUnitZoneRule
    {
        [HiddenInput(DisplayValue = true)]
        public int? IDDeviceFarmUnitZoneRule { get; set; }
        public int TenantID { get; set; }
        public int? DeviceFarmID { get; set; }
        public int? DeviceFarmUnitID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }
        public ActionType ActionType { get; set; } = ActionType.Relay;
        /// Required when ActionType is Relay, null when Notification.
        public RelayFunction? RelayFunction { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public ConditionNode? Root { get; set; }
        /// Roadmap #396(5) - survives RuleHierarchyResolver's normal scope-override even when a more specific scope has its own rule(s) for the same function/name; ORs in alongside whichever rule "won" (a zone rule can no longer silently erase a global frost-guard).
        public bool IsSafetyRule { get; set; }
        /// Notification-action only; supports {zone}/{value}/{metric} placeholders, substituted by RuleNotificationEvaluator ({value}/{metric} resolve from the first ComparisonNode found in the tree, best-effort for a multi-metric rule).
        public string? NotificationSubject { get; set; }
        public string? NotificationBody { get; set; }
    }

    /// Per-sensor-type average from each device's LATEST reading only, not a historical average (which would skew by poll frequency); null means nothing in scope has reported that type.
    public class SensorAverages
    {
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        /// Derived from Temperature+Humidity (api.Utils.VpdCalculator) - null whenever either is, never computed from a stale pairing.
        public double? Vpd { get; set; }
        /// Derived from Temperature+Humidity (api.Utils.DewPointCalculator, roadmap #396(4)).
        public double? DewPoint { get; set; }
        /// Temperature minus DewPoint - a shrinking spread (typically &lt;2-3°C) is an early condensation/fungal-disease signal.
        public double? DewPointSpread { get; set; }
        public double? Moisture { get; set; }
        public double? Light { get; set; }
        public double? Co2 { get; set; }
        public double? Tvoc { get; set; }
        public double? Barometer { get; set; }
        public double? LiquidPH { get; set; }
        public double? RainLevel { get; set; }
        public double? WaterLevel { get; set; }
        public double? Wind { get; set; }
        /// Derived from WaterLevel + the zone's tank calibration (api.Utils.TankCalculator) - null for a Unit rollup (spans zones with potentially different/no calibration) or an uncalibrated zone.
        public double? TankFillPercent { get; set; }
        public double? TankVolumeLiters { get; set; }
    }

    /// Traffic-light health for a Unit/Zone cube; Red beats Orange beats Green, so one offline device reddens the whole cube.
    public enum ZoneStatus
    {
        Green = 0,
        Orange = 1,
        Red = 2,
    }

    /// Last-24h hourly average per sensor type for the cube's sparklines - index 0 is the oldest bucket (24h ago), index 23 is the current (possibly partial) hour; a null bucket renders as a gap, not zero.
    public class SensorTrend
    {
        public const int HourBuckets = 24;

        public double?[] Temperature { get; set; } = new double?[HourBuckets];
        public double?[] SoilTemperature { get; set; } = new double?[HourBuckets];
        public double?[] Humidity { get; set; } = new double?[HourBuckets];
        public double?[] Vpd { get; set; } = new double?[HourBuckets];
        /// Derived from Temperature+Humidity (api.Utils.DewPointCalculator) - added for roadmap #398(1)'s RateOfChange node, so every SensorMetric (not just the raw ones) has a bucketed history to compare against.
        public double?[] DewPoint { get; set; } = new double?[HourBuckets];
        public double?[] DewPointSpread { get; set; } = new double?[HourBuckets];
        public double?[] Moisture { get; set; } = new double?[HourBuckets];
        public double?[] Light { get; set; } = new double?[HourBuckets];
        public double?[] Co2 { get; set; } = new double?[HourBuckets];
        public double?[] Tvoc { get; set; } = new double?[HourBuckets];
        public double?[] Barometer { get; set; } = new double?[HourBuckets];
        public double?[] LiquidPH { get; set; } = new double?[HourBuckets];
        public double?[] RainLevel { get; set; } = new double?[HourBuckets];
        public double?[] WaterLevel { get; set; } = new double?[HourBuckets];
        public double?[] Wind { get; set; } = new double?[HourBuckets];
    }

    /// One non-critical problem alert behind a Unit/Zone's Orange status - shown in _ZoneStatusBadge's dropdown, dismissable via DeviceApiController.DeviceEventAcknowledge.
    public class UnitZoneProblemAlert
    {
        public int IDEventDevice { get; set; }
        public int DeviceID { get; set; }
        public string? DeviceName { get; set; }
        public string? EventType { get; set; }
        public DateTimeOffset? Date { get; set; }
        public string? Message { get; set; }
    }

    /// One Unit cube on the top-level dashboard - name plus a roll-up over every sensor in every zone of this unit.
    public class DeviceFarmUnitDashboard
    {
        public int IDDeviceFarmUnit { get; set; }
        public string? DeviceFarmUnitName { get; set; }
        // Roadmap #412 (4) - null means unassigned; only used to group units by farm on the dashboard once a tenant has a second farm.
        public int? DeviceFarmID { get; set; }
        public int ZoneCount { get; set; }
        public int DeviceCount { get; set; }
        public SensorAverages Averages { get; set; } = new();
        public ZoneStatus Status { get; set; }
        public SensorTrend Trend { get; set; } = new();
        public IList<UnitZoneProblemAlert> ProblemAlerts { get; set; } = new List<UnitZoneProblemAlert>();
    }

    /// One Zone cube inside a Unit's drill-down, same shape as DeviceFarmUnitDashboard narrowed to this zone; Devices is populated only by the single-zone detail view, left empty on the zone-list view.
    public class DeviceFarmUnitZoneDashboard
    {
        public int IDDeviceFarmUnitZone { get; set; }
        public int IDDeviceFarmUnit { get; set; }
        public string? DeviceFarmUnitZoneName { get; set; }
        public int DeviceCount { get; set; }
        public SensorAverages Averages { get; set; } = new();
        public IList<Device> Devices { get; set; } = new List<Device>();
        public ZoneStatus Status { get; set; }
        public SensorTrend Trend { get; set; } = new();
        public IList<UnitZoneProblemAlert> ProblemAlerts { get; set; } = new List<UnitZoneProblemAlert>();
    }

    /// Body of the Add Controller/Add Sensor action - assigns an unassigned device to a zone; the zone's own DeviceFarmUnitID resolves the Unit, so no separate unit id is needed.
    public class DeviceZoneAssignment
    {
        public int IDDevice { get; set; }
        public int IDDeviceFarmUnitZone { get; set; }
    }
}
