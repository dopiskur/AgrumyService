using Agrumy.Shared.Models;

namespace Agrumy.Api.Devices
{
    /// Resolves the CSS-cascade-style Zone>Unit>Farm>Global(per-tenant) rule precedence for one zone's rules (roadmap #384 added the Farm tier) - a scope's rules for a function/name fully replace (not merge with) a less specific scope's, they never combine, UNLESS a rule is IsSafetyRule (roadmap #396(5)) which always survives regardless of scope. ResolveRelayRules groups by RelayFunction (called from DeviceConfigBuilder, output goes to firmware); ResolveNotificationRules groups by Name, not SensorMetric (#396(4) - a rule's conditions can now span several metrics, so metric is no longer a meaningful override key; called from Agrumy.Api.BackgroundWorkers.RuleNotificationEvaluator, server-side only).
    public static class RuleHierarchyResolver
    {
        public static IList<DeviceFarmUnitZoneRule> ResolveRelayRules(IList<DeviceFarmUnitZoneRule> zoneRules, IList<DeviceFarmUnitZoneRule> unitRules, IList<DeviceFarmUnitZoneRule> farmRules, IList<DeviceFarmUnitZoneRule> globalRules)
        {
            var result = new List<DeviceFarmUnitZoneRule>();
            var includedIds = new HashSet<int?>();
            foreach (RelayFunction function in Enum.GetValues<RelayFunction>())
            {
                IList<DeviceFarmUnitZoneRule> winner =
                    RulesFor(zoneRules, function) is { Count: > 0 } zoneMatch ? zoneMatch :
                    RulesFor(unitRules, function) is { Count: > 0 } unitMatch ? unitMatch :
                    RulesFor(farmRules, function) is { Count: > 0 } farmMatch ? farmMatch :
                    RulesFor(globalRules, function);
                foreach (DeviceFarmUnitZoneRule rule in winner)
                {
                    if (includedIds.Add(rule.IDDeviceFarmUnitZoneRule))
                    {
                        result.Add(rule);
                    }
                }
                // Roadmap #396(5) - a safety rule for this function, at ANY scope, always survives even when a more specific scope's own rules already won above; it ORs in alongside them (several rules for the same function already OR, unchanged), so a zone rule can no longer silently erase a global frost-guard.
                foreach (DeviceFarmUnitZoneRule safetyRule in zoneRules.Concat(unitRules).Concat(farmRules).Concat(globalRules)
                    .Where(r => r.ActionType == ActionType.Relay && r.RelayFunction == function && r.IsSafetyRule))
                {
                    if (includedIds.Add(safetyRule.IDDeviceFarmUnitZoneRule))
                    {
                        result.Add(safetyRule);
                    }
                }
            }
            return result;
        }

        private static List<DeviceFarmUnitZoneRule> RulesFor(IList<DeviceFarmUnitZoneRule> rules, RelayFunction function) =>
            rules.Where(r => r.ActionType == ActionType.Relay && r.RelayFunction == function).ToList();

        /// Same Zone>Unit>Farm>Global precedence as ResolveRelayRules, but for one zone's effective Notification-action rules, grouped by Name (roadmap #396(4) - a more specific scope's rule with the SAME Name replaces a less specific one; different names always coexist).
        public static IList<DeviceFarmUnitZoneRule> ResolveNotificationRules(IList<DeviceFarmUnitZoneRule> zoneRules, IList<DeviceFarmUnitZoneRule> unitRules, IList<DeviceFarmUnitZoneRule> farmRules, IList<DeviceFarmUnitZoneRule> globalRules)
        {
            var allNotification = zoneRules.Concat(unitRules).Concat(farmRules).Concat(globalRules)
                .Where(r => r.ActionType == ActionType.Notification).ToList();
            var names = allNotification.Select(r => r.Name).Distinct();

            var result = new List<DeviceFarmUnitZoneRule>();
            var includedIds = new HashSet<int?>();
            foreach (string name in names)
            {
                IList<DeviceFarmUnitZoneRule> winner =
                    NotificationRulesFor(zoneRules, name) is { Count: > 0 } zoneMatch ? zoneMatch :
                    NotificationRulesFor(unitRules, name) is { Count: > 0 } unitMatch ? unitMatch :
                    NotificationRulesFor(farmRules, name) is { Count: > 0 } farmMatch ? farmMatch :
                    NotificationRulesFor(globalRules, name);
                foreach (DeviceFarmUnitZoneRule rule in winner)
                {
                    if (includedIds.Add(rule.IDDeviceFarmUnitZoneRule))
                    {
                        result.Add(rule);
                    }
                }
            }
            // Roadmap #396(5) - same safety-rule survival as ResolveRelayRules above.
            foreach (DeviceFarmUnitZoneRule safetyRule in allNotification.Where(r => r.IsSafetyRule))
            {
                if (includedIds.Add(safetyRule.IDDeviceFarmUnitZoneRule))
                {
                    result.Add(safetyRule);
                }
            }
            return result;
        }

        private static List<DeviceFarmUnitZoneRule> NotificationRulesFor(IList<DeviceFarmUnitZoneRule> rules, string name) =>
            rules.Where(r => r.ActionType == ActionType.Notification && r.Name == name).ToList();
    }
}
