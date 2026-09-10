using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Shared.Models
{
    /// Roadmap #384 - top-level organizational grouping ABOVE Unit within the same tenant (a physical farm/site, e.g. separate LoRa networks or gateways naturally map to separate Farms); optional, a DeviceFarmUnit not yet assigned to one has DeviceFarmID null.
    public class DeviceFarm
    {
        [HiddenInput(DisplayValue = true)]
        public int? IDDeviceFarm { get; set; }
        public int? TenantID { get; set; }
        public string? DeviceFarmName { get; set; }
        /// Chosen once at creation - routes this farm's children to DeviceFarmUnit/DeviceFarmUnitZone (Greenhouse) or FarmOpenfieldCrop/FarmOpenfieldCropParcel (OpenField).
        public FarmType FarmType { get; set; } = FarmType.Greenhouse;
        // Card position on the Farms page, drag-and-drop reorderable - a new farm gets max+1 (bottom), not touched by anything else.
        public int DisplayOrder { get; set; }
        // Roadmap #409 - null unless this came from the Recycle Bin listing.
        public DateTimeOffset? DeletedAtUtc { get; set; }
        // Roadmap #427 - null unless this came from the pending-purge listing.
        public DateTimeOffset? PurgedAtUtc { get; set; }
    }

    /// A physical/logical space (e.g. a greenhouse) containing DeviceFarmUnitZones.
    public class DeviceFarmUnit : IFarmMidLevelNode
    {
        [HiddenInput(DisplayValue = true)]
        public int? IDDeviceFarmUnit { get; set; }
        public int? TenantID { get; set; }
        public string? DeviceFarmUnitName { get; set; }
        // Roadmap #384 - optional (a Farm-less Unit stays valid, no default-farm backfill).
        public int? DeviceFarmID { get; set; }
        // Cube position within its farm/unassigned grouping on the Farms page, drag-and-drop reorderable - a new unit gets max+1 (bottom), not touched by anything else.
        public int DisplayOrder { get; set; }

        int? IFarmMidLevelNode.Id => IDDeviceFarmUnit;
        int? IFarmMidLevelNode.FarmID => DeviceFarmID;
        string? IFarmMidLevelNode.Name => DeviceFarmUnitName;
    }

    /// A growing zone within one DeviceFarmUnit - "one zone = one controller" at most, may be sensor-only; TenantID is denormalized from DeviceFarmUnit so a zone query needs no join to check ownership.
    public class DeviceFarmUnitZone : IFarmLeafLevelNode
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

        // Tank calibration (roadmap #234) - all three null means "no tank tracking for this zone", not a zero-capacity tank. TankFillPercent/TankVolumeLiters (Agrumy.Shared.Utils.TankCalculator) are derived from these plus the zone's latest WaterLevel, never stored.
        public double? TankCapacityLiters { get; set; }
        /// Raw sensorData.WaterLevel reading when the tank is empty - not necessarily 0, depends on the physical sensor.
        public int? WaterLevelRawEmpty { get; set; }
        /// Raw sensorData.WaterLevel reading when the tank is full.
        public int? WaterLevelRawFull { get; set; }

        // Dry-run protection - blocks WaterPump (device-side, covers Interval/Schedule/Manual too, not just Threshold) below this fill percent. Null/<=0, or WaterLevelRawEmpty==WaterLevelRawFull (no tank calibration), disables it - a Water Valve zone with no tank sensor to protect.
        public double? WaterPumpMinLevel { get; set; }

        // Roadmap #219 - generalizes WaterPumpMaxRunSeconds above to the other two manually-triggerable functions; only ever used to compute a manual command's hard ExpiresAtUtc cap (Agrumy.Api.Commands.ManualActuateService), not applied to automated rule-driven runs the way WaterPump's own cap is.
        public int? HeatingMaxRunSeconds { get; set; }
        public int? VentilationMaxRunSeconds { get; set; }

        /// What a Heating rule does while its temperature reading is stale (sensor absent/disabled/failed) - null means Hold, the long-standing device-side default (see AgrumyFirmware's ActuatorController::evaluateRule).
        public HeatingFailSafePolicyType? HeatingFailSafePolicy { get; set; }

        // Roadmap #238 - admin-arranged dashboard widgets for this zone's own detail page, in display order. Never null (empty list means "show the default layout only") - see EfDeviceFarmUnitRepository's (de)serialization, same JSON-blob-at-the-app-layer convention as DeviceFarmUnitZoneRule.RootConditionJson. Stored server-side (not per-viewer) so a future mobile client renders the exact same layout, same reasoning the roadmap gave for this design.
        public List<DashboardWidget> DashboardWidgets { get; set; } = [];

        int? IFarmLeafLevelNode.Id => IDDeviceFarmUnitZone;
        int IFarmLeafLevelNode.MidLevelID => DeviceFarmUnitID;
        string? IFarmLeafLevelNode.Name => DeviceFarmUnitZoneName;
    }

    public enum DashboardWidgetType
    {
        SensorValue = 1,
        SensorTrend = 2,
        RelayStatus = 3,
        Text = 4,
    }

    /// One tile on a Zone's customizable dashboard - only the fields matching Type are meaningful (flat, tagged-union style, same convention as AgrumyFirmware's wire structs). Label is required for Text, optional elsewhere (overrides the auto-generated title, e.g. "Metric" -> its own name). SensorValue/SensorTrend read AggregationLevel+LevelID (a specific node id, independent of which zone's page the widget is displayed on); RelayStatus's LevelID is always a zone/parcel id.
    public class DashboardWidget
    {
        public DashboardWidgetType Type { get; set; }
        public SensorMetric? Metric { get; set; }
        public RelayFunction? RelayFunction { get; set; }
        public HierarchyNodeKind? AggregationLevel { get; set; }
        public int? LevelID { get; set; }
        public string? Label { get; set; }
    }

    /// GET target for one widget's live data - Averages+Trend only, not the full DeviceFarmUnitDashboard/DeviceFarmUnitZoneDashboard shape, since a widget tile needs neither device counts nor problem alerts.
    public class DashboardAggregate
    {
        public SensorAverages Averages { get; set; } = new();
        public SensorTrend Trend { get; set; } = new();
    }

    /// Relay function a DeviceFarmUnitZoneRule targets, same numeric convention as deviceTypeRelay seed rows; kept as a plain int on the wire (not this enum) so firmware can parse it as a number without JsonStringEnumConverter. Every function folds through the same DeviceFarmUnitZoneRule.TargetPercent/MAX engine - RelayFunctionKind.IsPositional now only distinguishes Screen/Vent for feature-level rules that are inherently positional (e.g. ApplyDayNightPreset), not the fold itself.
    public enum RelayFunction
    {
        Ventilation = 1,
        Light = 2,
        Heating = 3,
        WaterPump = 4,
        Screen = 5,
        Vent = 6,
    }

    public static class RelayFunctionKind
    {
        /// True for Screen/Vent - still meaningful for features that are inherently positional (e.g. ApplyDayNightPreset rejects them), even though every function now folds through the same TargetPercent/MAX engine.
        public static bool IsPositional(this RelayFunction function) => function is RelayFunction.Screen or RelayFunction.Vent;
    }

    /// One entry in POST /api/ControllerData's array - sent every time a relay's on/off state actually CHANGES, not on a fixed interval like SensorData; a real device pushes this alongside a physical relay flip, a simulated one alongside its calculated equivalent, same wire shape either way.
    public class ControllerDataPush
    {
        public RelayFunction RelayFunction { get; set; }
        public bool IsOn { get; set; }
        /// The rule fold's target percent (0-100) this tick, for every relay function now - IsOn is just Percent &gt; 0.
        public int? Percent { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
    }

    /// Current on/off state for one RelayFunction on one device - GET /api/ControllerData's shape, and what DeviceFleetStatus.RelayStates carries.
    public class ControllerDataStatus
    {
        public RelayFunction RelayFunction { get; set; }
        public bool IsOn { get; set; }
        /// Same meaning as ControllerDataPush.Percent - null for a binary function.
        public int? Percent { get; set; }
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
        /// DERIVED (Agrumy.Shared.Utils.DewPointCalculator, Magnus formula) - Temperature+Humidity.
        DewPoint = 14,
        /// DERIVED - Temperature minus DewPoint; a small/shrinking spread is an early condensation/fungal-disease signal, distinct from absolute humidity alone.
        DewPointSpread = 15,
        /// RAW - electrical conductivity, ADS1115Ec.
        Ec = 16,
        /// RAW - load cell reading, HX711.
        Weight = 17,
    }

    /// What a rule does once its Conditions fold to true - Relay is evaluated on-device (AgrumyFirmware's ActuatorController), Notification is evaluated server-side (Agrumy.Api.BackgroundWorkers.RuleNotificationEvaluator) since firmware has no notification capability.
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
        /// Never reaches evaluation as-is on either action path - Agrumy.Rules.AstronomicalRuleResolver compiles every occurrence (anywhere in the tree) into an effective Schedule node for today's local date first (Relay: before the device config is sent; Notification: resolved server-side each tick).
        Astronomical = 5,
        /// Only valid inside a Notification-action rule - a Relay-action rule fires invisibly on-device, so the server has no way to observe it as a trigger.
        RuleTriggered = 6,
        /// Roadmap #398(1) - only valid inside a Notification-action rule; compares a live reading against Agrumy.Shared.Models.SensorTrend's hourly history, which only the server (not firmware) has.
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

    /// What a Heating-targeting rule does while its temperature reading is stale, past the point AgrumyFirmware's own MAX_HEATING_SENSOR_STALE_SECONDS grace window closes - per-zone instead of one fixed device-wide constant, since a frost-sensitive greenhouse and a tolerant one want different tradeoffs.
    public enum HeatingFailSafePolicyType
    {
        /// Today's long-standing default: hold the last on/off state through the grace window, then force off. Risks staying on too long if the sensor genuinely failed, but never leaves a real cold snap unheated over a brief NTP/I2C hiccup.
        Hold = 0,
        /// Forces off the instant the reading goes stale, no grace window - the conservative choice for a zone where a stuck-on heater is the worse failure mode (e.g. no one on-site to notice).
        Off = 1,
        /// Falls back to the rule's Schedule/Interval nodes only (a Comparison node naturally evaluates false on a NaN reading, the same generic behavior every non-Heating function already gets) - for a zone whose Heating rule already has a schedule fallback baked in.
        ScheduleOnly = 2,
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

    /// Response of POST .../Rule (Zone/Unit/Farm/Global scope) - ScopeConflictWarning is non-null only when this rule's RelayFunction/Name already has a rule at a DIFFERENT scope somewhere in its ancestor/descendant chain (see Agrumy.Api.Devices.RuleScopeConflictService); purely informational, the rule is saved either way.
    public class RuleAddResult
    {
        public int IDDeviceFarmUnitZoneRule { get; set; }
        public string? ScopeConflictWarning { get; set; }
    }

    /// Request for POST .../Zone/ApplyDayNightPreset. DayStartSeconds/DayEndSeconds mark the day window (0-86399/1-86400, DayStartSeconds &lt; DayEndSeconds); night is the complement, computed server-side (Agrumy.Rules.DayNightTargetPresetBuilder). Screen/Vent aren't valid Function values here - a day/night target is a plain on/off threshold, not a positional TargetPercent.
    public class DayNightTargetPresetRequest
    {
        public RelayFunction Function { get; set; }
        public SensorMetric Metric { get; set; }
        public ComparisonOperator Operator { get; set; }
        public double DayValue { get; set; }
        public double NightValue { get; set; }
        public double Hysteresis { get; set; }
        public int DayStartSeconds { get; set; }
        public int DayEndSeconds { get; set; }
        public string NamePrefix { get; set; } = "";
    }

    /// Response of POST .../Zone/ApplyDayNightPreset - same "capped, not failed" shape as HorticultureCatalogApplyResult, since the same zone rule-count cap can stop the night rule after the day rule already saved.
    public class DayNightPresetApplyResult
    {
        public int RulesAdded { get; set; }
        public IList<string> RulesSkipped { get; set; } = [];
    }

    /// One automation rule at exactly one scope - DeviceFarmUnitZoneID set means Zone scope, DeviceFarmUnitID set means Unit scope, DeviceFarmID set means Farm scope, DeviceFarmOpenfieldCropParcelID/DeviceFarmOpenfieldCropID mean Parcel/Crop scope, SimulationSessionID set means Simulation scope, ExperimentID set means Experiment scope, all null means Global (per-tenant). Several rules at the SAME scope for the same RelayFunction still fold together by taking the MAX of their TargetPercent; Notification rules override by Name instead (a more specific scope's rule with the SAME Name replaces a less specific one, different names always coexist) since a rule's conditions can now span several metrics. IsSafetyRule rules always survive being overridden regardless of scope - see Agrumy.Rules.RuleHierarchyResolver.
    public class DeviceFarmUnitZoneRule : IValidatableObject
    {
        [HiddenInput(DisplayValue = true)]
        public int? IDDeviceFarmUnitZoneRule { get; set; }
        public int TenantID { get; set; }
        public int? DeviceFarmID { get; set; }
        public int? DeviceFarmUnitID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }
        public int? DeviceFarmOpenfieldCropID { get; set; }
        public int? DeviceFarmOpenfieldCropParcelID { get; set; }
        /// A device evaluates this ahead of its real Zone>Unit>Farm>Global rules while it's a member of this session, falling back to that real hierarchy for any RelayFunction/Name this scope has no rule for - lets a simulation test rule logic without a gap in coverage silently doing nothing.
        public int? SimulationSessionID { get; set; }
        /// Same "evaluated ahead of the real hierarchy, falls back for anything uncovered" precedence as SimulationSessionID, one tier below it - see RuleHierarchyResolver's Simulation&gt;Experiment&gt;Zone&gt;Unit&gt;Farm&gt;Global order. Unlike Simulation this controls real devices, so its rules are a real A/B test, not a sandboxed dry run.
        public int? ExperimentID { get; set; }
        public ActionType ActionType { get; set; } = ActionType.Relay;
        /// Required when ActionType is Relay, null when Notification.
        public RelayFunction? RelayFunction { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public ConditionNode? Root { get; set; }
        /// Required for every Relay rule regardless of function - the 0-100 demand this rule asserts while Root evaluates true, 0 while false. Several simultaneously-true rules for the same function resolve to the HIGHEST TargetPercent among them, not an OR/AND - see AgrumyFirmware's foldTargetPercent and Agrumy.Api.Devices.SimulatedRelayEvaluator.EvaluatePercent.
        public int? TargetPercent { get; set; }
        /// Survives RuleHierarchyResolver's normal scope-override even when a more specific scope has its own rule(s) for the same function/name; ORs in alongside whichever rule "won" (a zone rule can no longer silently erase a global frost-guard) - EXCEPT when Simulation itself wins the function, a deliberate exception so a simulated scenario can actually test whether the safety rule fires.
        public bool IsSafetyRule { get; set; }
        /// Notification-action only; supports {zone}/{value}/{metric} placeholders, substituted by RuleNotificationEvaluator ({value}/{metric} resolve from the first ComparisonNode found in the tree, best-effort for a multi-metric rule).
        public string? NotificationSubject { get; set; }
        public string? NotificationBody { get; set; }

        // Must match AgrumyFirmware Logic/ConditionTree.h's MAX_NODES_PER_RULE - total node count across the WHOLE tree (leaves+groups), not just top-level conditions. Kept small deliberately (DRAM budget on-device), see that constant's own remarks.
        public const int HardMaxNodesPerRule = 8;
        // Must match AgrumyFirmware Logic/ConditionTree.h's MAX_CHILDREN_PER_GROUP.
        public const int HardMaxChildrenPerGroup = 4;

        /// Shape+bound self-validation - everything Agrumy.Api.Devices.RuleValidationService.ShapeErrorAsync checks EXCEPT RuleTriggered's cross-reference (needs a DB lookup, which a Shared-project DTO can't do; RuleValidationService still checks that part itself). Runs automatically wherever this DTO is model-bound ([ApiController] rejects it with 400 before the action body runs) or wherever RuleValidationService.ShapeErrorAsync delegates to it - a rule built in code (e.g. Agrumy.Rules.HorticultureRuleTemplateBuilder) gets the exact same checks as one typed in by hand, not a second hand-maintained copy of them.
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                yield return new ValidationResult("Name is required.", [nameof(Name)]);
            }
            if (ActionType == ActionType.Relay)
            {
                if (RelayFunction == null)
                {
                    yield return new ValidationResult("Relay rule: relayFunction is required.", [nameof(RelayFunction)]);
                }
                else if (TargetPercent is not int percent || percent < 0 || percent > 100)
                {
                    yield return new ValidationResult("Relay rule: targetPercent is required, 0-100.", [nameof(TargetPercent)]);
                }
            }
            else
            {
                if (RelayFunction != null)
                {
                    yield return new ValidationResult("Notification rule: relayFunction must not be set.", [nameof(RelayFunction)]);
                }
                if (TargetPercent != null)
                {
                    yield return new ValidationResult("Notification rule: targetPercent must not be set.", [nameof(TargetPercent)]);
                }
                if (string.IsNullOrWhiteSpace(NotificationSubject))
                {
                    yield return new ValidationResult("Notification rule: subject is required.", [nameof(NotificationSubject)]);
                }
            }

            if (Root == null)
            {
                yield return new ValidationResult("A rule needs at least one condition.", [nameof(Root)]);
                yield break;
            }
            if (CountNodes(Root) > HardMaxNodesPerRule)
            {
                yield return new ValidationResult($"A rule may have at most {HardMaxNodesPerRule} conditions/groups total.", [nameof(Root)]);
                yield break; // tree too large to usefully walk further
            }
            foreach (ValidationResult error in NodeErrors(Root, ActionType))
            {
                yield return error;
            }
        }

        private static int CountNodes(ConditionNode node) => 1 + node.Children.Sum(CountNodes);

        /// Recurses into GroupNode.Children - a rule's tree can nest arbitrarily, so every node (not just top-level) needs the same shape/bound checks. Skips RuleTriggered's own referenced-rule existence check - that needs a DB lookup, done separately by RuleValidationService.
        private static IEnumerable<ValidationResult> NodeErrors(ConditionNode node, ActionType actionType)
        {
            if (node.Type == NodeType.RuleTriggered && actionType != ActionType.Notification)
            {
                yield return new ValidationResult("\"another rule fired\" is only valid on a Notification-action rule (a Relay rule fires on-device, invisibly to the server).", [nameof(Root)]);
            }
            if ((node.Type == NodeType.RateOfChange || node.Type == NodeType.DifDisruption) && actionType != ActionType.Notification)
            {
                yield return new ValidationResult("A rate-of-change/DIF condition is only valid on a Notification-action rule - it reads SensorTrend history the device never receives, so a Relay rule would always evaluate this condition as false.", [nameof(Root)]);
            }
            if (NodeConfigError(node) is string configError)
            {
                yield return new ValidationResult(configError, [nameof(Root)]);
            }
            if (node.Type == NodeType.Group)
            {
                if (node.Children.Count == 0)
                {
                    yield return new ValidationResult("A group needs at least one child condition.", [nameof(Root)]);
                }
                else if (node.Children.Count > HardMaxChildrenPerGroup)
                {
                    yield return new ValidationResult($"A group may have at most {HardMaxChildrenPerGroup} direct children.", [nameof(Root)]);
                }
                if (node.GroupOperator == null)
                {
                    yield return new ValidationResult("A group needs an AND/OR operator.", [nameof(Root)]);
                }
                foreach (ConditionNode child in node.Children)
                {
                    foreach (ValidationResult childError in NodeErrors(child, actionType))
                    {
                        yield return childError;
                    }
                }
            }
        }

        /// Shape+bound check per NodeType - the firmware would otherwise silently treat a malformed rule as inert (ConfigParser/evaluateRule), a confusing way to discover a typo; a ComparisonNode's Value1 is deliberately unbounded, only Hysteresis has a universal "must not be negative" rule. RuleTriggered's own referencedRuleId presence is checked here (cheap, no DB); its existence/tenant/action-type cross-reference is not (see RuleValidationService).
        private static string? NodeConfigError(ConditionNode node)
        {
            switch (node.Type)
            {
                case NodeType.Comparison:
                    if (node.Metric == null) { return "metric is required."; }
                    if (node.Operator == null) { return "operator is required."; }
                    if (node.Value1 == null) { return "value is required."; }
                    if (node.Operator == ComparisonOperator.Between && node.Value2 == null) { return "a second value is required for \"between\"."; }
                    if (node.Hysteresis is < 0) { return "hysteresis must not be negative."; }
                    return null;
                case NodeType.Interval:
                    if (node.Interval is not int interval || interval <= 0) { return "interval must be greater than 0."; }
                    if (node.IntervalLength is not int intervalLength || intervalLength <= 0 || intervalLength > interval) { return "on-duration must be greater than 0 and not exceed the interval."; }
                    return null;
                case NodeType.Schedule:
                    if (node.DaysOfWeek is not int scheduleDays || scheduleDays < 0 || scheduleDays > 0b1111111) { return "days of week must be a value from 0 to 127."; }
                    if (node.Start is not int start || start < 0 || start > 86399) { return "start must be between 0 and 86399 seconds since local midnight."; }
                    if (node.Duration is not int duration || duration < 1 || start + duration > 86400) { return "duration must be at least 1 second and not cross local midnight (start + duration <= 86400)."; }
                    return null;
                case NodeType.Astronomical:
                    if (node.DaysOfWeek is not int astroDays || astroDays < 0 || astroDays > 0b1111111) { return "days of week must be a value from 0 to 127."; }
                    if (node.SunriseOffsetMinutes is not int sunriseOffset || sunriseOffset < -720 || sunriseOffset > 720
                        || node.SunsetOffsetMinutes is not int sunsetOffset || sunsetOffset < -720 || sunsetOffset > 720)
                    {
                        return "offsets must be between -720 and 720 minutes.";
                    }
                    return null;
                case NodeType.RuleTriggered:
                    return node.ReferencedRuleId == null ? "referencedRuleId is required." : null;
                case NodeType.RateOfChange:
                    if (node.Metric == null) { return "metric is required."; }
                    if (node.WindowHours is not int rocWindow || rocWindow < 1 || rocWindow >= SensorTrend.HourBuckets) { return $"windowHours must be between 1 and {SensorTrend.HourBuckets - 1}."; }
                    if (node.ChangeThreshold is not double rocThreshold || rocThreshold < 0) { return "changeThreshold is required and must not be negative."; }
                    return null;
                case NodeType.DifDisruption:
                    if (node.NightWindowHours is not int nightHours || nightHours < 1) { return "nightWindowHours must be at least 1."; }
                    if (node.DayWindowHours is not int dayHours || dayHours < 1) { return "dayWindowHours must be at least 1."; }
                    if (nightHours + dayHours > SensorTrend.HourBuckets) { return $"nightWindowHours + dayWindowHours must not exceed {SensorTrend.HourBuckets}."; }
                    if (node.MinDifDegrees == null) { return "minDifDegrees is required."; }
                    return null;
                case NodeType.Group:
                    return null; // Children/GroupOperator checked by the caller (NodeErrors), not here.
                default:
                    return "unknown condition type.";
            }
        }
    }

    /// Per-sensor-type average from each device's LATEST reading only, not a historical average (which would skew by poll frequency); null means nothing in scope has reported that type.
    public class SensorAverages
    {
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        /// Derived from Temperature+Humidity (Agrumy.Shared.Utils.VpdCalculator) - null whenever either is, never computed from a stale pairing.
        public double? Vpd { get; set; }
        /// Derived from Temperature+Humidity (Agrumy.Shared.Utils.DewPointCalculator, roadmap #396(4)).
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
        public double? Ec { get; set; }
        public double? Weight { get; set; }
        /// Derived from WaterLevel + the zone's tank calibration (Agrumy.Shared.Utils.TankCalculator) - null for a Unit rollup (spans zones with potentially different/no calibration) or an uncalibrated zone.
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
        /// Derived from Temperature+Humidity (Agrumy.Shared.Utils.DewPointCalculator) - added for roadmap #398(1)'s RateOfChange node, so every SensorMetric (not just the raw ones) has a bucketed history to compare against.
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
        public double?[] Ec { get; set; } = new double?[HourBuckets];
        public double?[] Weight { get; set; } = new double?[HourBuckets];
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
        // Null means unassigned; only used to group units by farm on the dashboard once a tenant has a second farm.
        public int? DeviceFarmID { get; set; }
        // Same drag-and-drop reorder field as DeviceFarmUnit.DisplayOrder - carried here too since this is the DTO the Farm page's cubes actually render.
        public int DisplayOrder { get; set; }
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
