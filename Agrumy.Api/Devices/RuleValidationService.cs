using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Devices
{
    /// Shape+bound validation for a DeviceFarmUnitZoneRule tree, shared by every controller that writes rules at any scope (DeviceFarmUnitApiController's Zone/Unit/Farm/Global routes, SimulationApiController's Simulation-scoped routes) - extracted so the two don't drift out of sync on what counts as a valid rule.
    public class RuleValidationService(IDeviceFarmUnitRepository deviceFarmUnitRepo)
    {
        // Must match AgrumyFirmware Logic/ConditionTree.h's MAX_NODES_PER_RULE - total node count across the WHOLE tree (leaves+groups), not just top-level conditions. Kept small deliberately (DRAM budget on-device), see that constant's own remarks.
        public const int HardMaxNodesPerRule = 8;
        // Must match AgrumyFirmware Logic/ConditionTree.h's MAX_CHILDREN_PER_GROUP.
        public const int HardMaxChildrenPerGroup = 4;

        /// Shape+bound check for the whole rule: ActionType/RelayFunction/Name consistency, tree-size bounds, per-node shape recursively, and (DB-dependent, hence async) RuleTriggered's cross-reference validity.
        public async Task<string?> ShapeErrorAsync(DeviceFarmUnitZoneRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                return "Name is required.";
            }
            if (rule.ActionType == ActionType.Relay)
            {
                if (rule.RelayFunction == null) { return "Relay rule: relayFunction is required."; }
            }
            else
            {
                if (rule.RelayFunction != null) { return "Notification rule: relayFunction must not be set."; }
                if (string.IsNullOrWhiteSpace(rule.NotificationSubject)) { return "Notification rule: subject is required."; }
            }

            if (rule.Root == null)
            {
                return "A rule needs at least one condition.";
            }
            int nodeCount = CountNodes(rule.Root);
            if (nodeCount > HardMaxNodesPerRule)
            {
                return $"A rule may have at most {HardMaxNodesPerRule} conditions/groups total.";
            }
            return await NodeShapeErrorAsync(rule.Root, rule, isRoot: true);
        }

        private static int CountNodes(ConditionNode node) => 1 + node.Children.Sum(CountNodes);

        /// Recurses into GroupNode.Children - a rule's tree can nest arbitrarily, so every node (not just top-level) needs the same shape/bound checks a flat condition list used to get once each.
        private async Task<string?> NodeShapeErrorAsync(ConditionNode node, DeviceFarmUnitZoneRule rule, bool isRoot)
        {
            if (node.Type == NodeType.RuleTriggered && rule.ActionType != ActionType.Notification)
            {
                return "\"another rule fired\" is only valid on a Notification-action rule (a Relay rule fires on-device, invisibly to the server).";
            }
            if ((node.Type == NodeType.RateOfChange || node.Type == NodeType.DifDisruption) && rule.ActionType != ActionType.Notification)
            {
                return "A rate-of-change/DIF condition is only valid on a Notification-action rule - it reads SensorTrend history the device never receives, so a Relay rule would always evaluate this condition as false.";
            }
            if (NodeConfigError(node) is string configError)
            {
                return configError;
            }
            if (node.Type == NodeType.RuleTriggered)
            {
                DeviceFarmUnitZoneRule? referenced = node.ReferencedRuleId is int refId ? await deviceFarmUnitRepo.RuleGetByIdAsync(refId) : null;
                if (referenced == null || referenced.TenantID != rule.TenantID || referenced.ActionType != ActionType.Notification)
                {
                    return "\"another rule fired\" must reference a rule that exists, belongs to the same tenant, and is a Notification-action rule.";
                }
            }
            if (node.Type == NodeType.Group)
            {
                if (node.Children.Count == 0)
                {
                    return "A group needs at least one child condition.";
                }
                if (node.Children.Count > HardMaxChildrenPerGroup)
                {
                    return $"A group may have at most {HardMaxChildrenPerGroup} direct children.";
                }
                if (node.GroupOperator == null)
                {
                    return "A group needs an AND/OR operator.";
                }
                foreach (ConditionNode child in node.Children)
                {
                    if (await NodeShapeErrorAsync(child, rule, isRoot: false) is string childError)
                    {
                        return childError;
                    }
                }
            }
            else if (isRoot)
            {
                // A non-Group root is fine (a rule with exactly one condition needs no wrapping group) - nothing further to check here.
            }
            return null;
        }

        /// Shape+bound check per NodeType - the firmware would otherwise silently treat a malformed rule as inert (ConfigParser/evaluateRule), a confusing way to discover a typo; a ComparisonNode's Value1 is deliberately unbounded, only Hysteresis has a universal "must not be negative" rule.
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
                    return null; // Children/GroupOperator checked by the caller (NodeShapeErrorAsync), not here.
                default:
                    return "unknown condition type.";
            }
        }
    }
}
