using Agrumy.Shared.Models;

namespace Agrumy.Rules
{
    /// Resolves the CSS-cascade-style Simulation>Experiment>Zone>Unit>Farm>Global(per-tenant) rule precedence for one zone's rules (the Simulation/Experiment tiers only ever have candidates for a device currently in scope of an active session/experiment, empty otherwise; Simulation outranks Experiment since Simulation's whole purpose is a consequence-free sandbox even for a device also under a long-running Experiment) - a scope's rules for a function/name fully replace (not merge with) a less specific scope's, they never combine, UNLESS a rule is IsSafetyRule which always survives regardless of scope - EXCEPT when Simulation itself wins a function/name: the sandbox must be able to test whether the safety rule actually works (e.g. simulate cold and check frost-guard fires), so it must not silently OR back in and mask the outcome. Experiment is a real device, not a sandbox, so it keeps the normal safety-survives behavior. ResolveRelayRules groups by RelayFunction (called from DeviceConfigBuilder, output goes to firmware); ResolveNotificationRules groups by Name, not SensorMetric (a rule's conditions can now span several metrics, so metric is no longer a meaningful override key; called from Agrumy.Api.BackgroundWorkers.RuleNotificationEvaluator, server-side only).
    public static class RuleHierarchyResolver
    {
        public static IList<DeviceFarmUnitZoneRule> ResolveRelayRules(IList<DeviceFarmUnitZoneRule> simulationRules, IList<DeviceFarmUnitZoneRule> experimentRules, IList<DeviceFarmUnitZoneRule> zoneRules, IList<DeviceFarmUnitZoneRule> unitRules, IList<DeviceFarmUnitZoneRule> farmRules, IList<DeviceFarmUnitZoneRule> globalRules)
        {
            var result = new List<DeviceFarmUnitZoneRule>();
            var includedIds = new HashSet<int?>();
            foreach (RelayFunction function in Enum.GetValues<RelayFunction>())
            {
                List<DeviceFarmUnitZoneRule> simMatch = RulesFor(simulationRules, function);
                IList<DeviceFarmUnitZoneRule> winner =
                    simMatch is { Count: > 0 } ? simMatch :
                    RulesFor(experimentRules, function) is { Count: > 0 } experimentMatch ? experimentMatch :
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
                // A safety rule for this function, at ANY scope, always survives even when a more specific scope's own rules already won above; it ORs in alongside them (several rules for the same function already MAX-fold, unchanged), so a zone rule can no longer silently erase a global frost-guard. EXCEPT when Simulation won this function: Simulation is a consequence-free sandbox specifically meant to let a scenario like "does frost-guard actually work?" be tested by simulating cold, so safety must NOT OR back in and mask that. Experiment still isn't Simulation - it drives a real device, so it keeps the normal safety-survives behavior below.
                if (simMatch.Count > 0)
                {
                    continue;
                }
                foreach (DeviceFarmUnitZoneRule safetyRule in simulationRules.Concat(experimentRules).Concat(zoneRules).Concat(unitRules).Concat(farmRules).Concat(globalRules)
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

        /// Same Simulation>Experiment>Zone>Unit>Farm>Global precedence as ResolveRelayRules, but for one zone's effective Notification-action rules, grouped by Name - a more specific scope's rule with the SAME Name replaces a less specific one; different names always coexist.
        public static IList<DeviceFarmUnitZoneRule> ResolveNotificationRules(IList<DeviceFarmUnitZoneRule> simulationRules, IList<DeviceFarmUnitZoneRule> experimentRules, IList<DeviceFarmUnitZoneRule> zoneRules, IList<DeviceFarmUnitZoneRule> unitRules, IList<DeviceFarmUnitZoneRule> farmRules, IList<DeviceFarmUnitZoneRule> globalRules)
        {
            var allNotification = simulationRules.Concat(experimentRules).Concat(zoneRules).Concat(unitRules).Concat(farmRules).Concat(globalRules)
                .Where(r => r.ActionType == ActionType.Notification).ToList();
            var names = allNotification.Select(r => r.Name).Distinct();

            var result = new List<DeviceFarmUnitZoneRule>();
            var includedIds = new HashSet<int?>();
            var simulationWonNames = new HashSet<string>();
            foreach (string name in names)
            {
                List<DeviceFarmUnitZoneRule> simMatch = NotificationRulesFor(simulationRules, name);
                IList<DeviceFarmUnitZoneRule> winner =
                    simMatch is { Count: > 0 } ? simMatch :
                    NotificationRulesFor(experimentRules, name) is { Count: > 0 } experimentMatch ? experimentMatch :
                    NotificationRulesFor(zoneRules, name) is { Count: > 0 } zoneMatch ? zoneMatch :
                    NotificationRulesFor(unitRules, name) is { Count: > 0 } unitMatch ? unitMatch :
                    NotificationRulesFor(farmRules, name) is { Count: > 0 } farmMatch ? farmMatch :
                    NotificationRulesFor(globalRules, name);
                if (simMatch.Count > 0)
                {
                    simulationWonNames.Add(name);
                }
                foreach (DeviceFarmUnitZoneRule rule in winner)
                {
                    if (includedIds.Add(rule.IDDeviceFarmUnitZoneRule))
                    {
                        result.Add(rule);
                    }
                }
            }
            // Same safety-rule survival as ResolveRelayRules above, and the same Simulation exception - a Name Simulation just won is exactly the "test whether the safety alert actually fires" scenario, so it must not have its own safety rule OR back in for that Name.
            foreach (DeviceFarmUnitZoneRule safetyRule in allNotification.Where(r => r.IsSafetyRule && !simulationWonNames.Contains(r.Name)))
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
