using Agrumy.Rules;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests;

/// Zone>Unit>Farm>Global precedence (roadmap #212/#384) - a scope's rules for a function (Relay) or name (Notification, #396(4)) fully replace, never merge with, a less specific scope's, UNLESS IsSafetyRule (#396(5)) which always survives regardless of scope.
public class RuleHierarchyResolverTests
{
    private static ConditionNode Leaf() => new() { Type = NodeType.Comparison, Metric = SensorMetric.Temperature, Operator = ComparisonOperator.GreaterThan, Value1 = 1, Hysteresis = 1 };

    private static DeviceFarmUnitZoneRule RelayRule(RelayFunction function, int marker, int? zoneId = null, int? unitId = null, int? farmId = null, int? simulationId = null, int? experimentId = null, bool isSafetyRule = false) => new()
    {
        IDDeviceFarmUnitZoneRule = marker,
        TenantID = 1,
        DeviceFarmUnitZoneID = zoneId,
        DeviceFarmUnitID = unitId,
        DeviceFarmID = farmId,
        SimulationSessionID = simulationId,
        ExperimentID = experimentId,
        ActionType = ActionType.Relay,
        RelayFunction = function,
        Name = "rule " + marker,
        IsSafetyRule = isSafetyRule,
        Root = Leaf(),
    };

    private static DeviceFarmUnitZoneRule NotificationRule(string name, int marker, int? zoneId = null, int? unitId = null, int? farmId = null, bool isSafetyRule = false) => new()
    {
        IDDeviceFarmUnitZoneRule = marker,
        TenantID = 1,
        DeviceFarmUnitZoneID = zoneId,
        DeviceFarmUnitID = unitId,
        DeviceFarmID = farmId,
        ActionType = ActionType.Notification,
        Name = name,
        IsSafetyRule = isSafetyRule,
        Root = Leaf(),
        NotificationSubject = "subject",
    };

    [Fact]
    public void ResolveRelayRules_NoZoneOrUnitRule_FarmWinsOverGlobal()
    {
        var farmRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Ventilation, 1, farmId: 7) };
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Ventilation, 2) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], [], [], farmRules, globalRules);

        Assert.Equal([1], result.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    [Fact]
    public void ResolveRelayRules_UnitRuleExists_UnitWinsOverFarm()
    {
        var unitRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Ventilation, 1, unitId: 9) };
        var farmRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Ventilation, 2, farmId: 7) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], [], unitRules, farmRules, []);

        Assert.Equal([1], result.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    [Fact]
    public void ResolveRelayRules_ZoneRuleExists_ZoneWinsOverUnitAndGlobal()
    {
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Light, 1, zoneId: 5) };
        var unitRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Light, 2, unitId: 9) };
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Light, 3) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], zoneRules, unitRules, [], globalRules);

        Assert.Equal([1], result.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    [Fact]
    public void ResolveRelayRules_NoZoneRule_FallsBackToUnit()
    {
        var unitRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 2, unitId: 9) };
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 3) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], [], unitRules, [], globalRules);

        Assert.Equal([2], result.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    [Fact]
    public void ResolveRelayRules_NoZoneOrUnitRule_FallsBackToGlobal()
    {
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.WaterPump, 3) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], [], [], [], globalRules);

        Assert.Equal([3], result.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    [Fact]
    public void ResolveRelayRules_SimulationRuleExists_SimulationWinsOverZone()
    {
        var simulationRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 1, simulationId: 42) };
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 2, zoneId: 5) };

        var result = RuleHierarchyResolver.ResolveRelayRules(simulationRules, [], zoneRules, [], [], []);

        Assert.Equal([1], result.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    [Fact]
    public void ResolveRelayRules_SimulationHasNoRuleForFunction_FallsBackToZone()
    {
        // Session 42 only overrides Heating - Light must still fall through to the real Zone rule, not silently do nothing.
        var simulationRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 1, simulationId: 42) };
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Light, 2, zoneId: 5) };

        var result = RuleHierarchyResolver.ResolveRelayRules(simulationRules, [], zoneRules, [], [], []);

        Assert.Equal([1, 2], result.Select(r => r.IDDeviceFarmUnitZoneRule).OrderBy(id => id));
    }

    /// Experiment sits one tier below Simulation (a sandboxed test still outranks a real, consequence-carrying experiment) but above Zone.
    [Fact]
    public void ResolveRelayRules_ExperimentRuleExists_ExperimentWinsOverZone_ButLosesToSimulation()
    {
        var simulationRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 1, simulationId: 42) };
        var experimentRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 2, experimentId: 7) };
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 3, zoneId: 5) };

        var result = RuleHierarchyResolver.ResolveRelayRules(simulationRules, experimentRules, zoneRules, [], [], []);
        Assert.Equal([1], result.Select(r => r.IDDeviceFarmUnitZoneRule));

        var withoutSimulation = RuleHierarchyResolver.ResolveRelayRules([], experimentRules, zoneRules, [], [], []);
        Assert.Equal([2], withoutSimulation.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    [Fact]
    public void ResolveRelayRules_ExperimentHasNoRuleForFunction_FallsBackToZone()
    {
        var experimentRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 1, experimentId: 7) };
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Light, 2, zoneId: 5) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], experimentRules, zoneRules, [], [], []);

        Assert.Equal([1, 2], result.Select(r => r.IDDeviceFarmUnitZoneRule).OrderBy(id => id));
    }

    [Fact]
    public void ResolveRelayRules_DifferentFunctionsResolveIndependently()
    {
        // Zone defines Light, but not Heating - Heating should still fall through to Global, Light stays Zone's own.
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Light, 1, zoneId: 5) };
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 3), RelayRule(RelayFunction.Light, 4) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], zoneRules, [], [], globalRules);

        Assert.Equal([1, 3], result.Select(r => r.IDDeviceFarmUnitZoneRule).OrderBy(x => x));
    }

    [Fact]
    public void ResolveRelayRules_MultipleZoneRulesSameFunction_AllSurvive_OrSemanticsPreserved()
    {
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Ventilation, 1, zoneId: 5), RelayRule(RelayFunction.Ventilation, 2, zoneId: 5) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], zoneRules, [], [], []);

        Assert.Equal(2, result.Count);
    }

    /// Roadmap #396(5) - a global frost-guard survives even though the zone's own rule for the same function wins normal resolution; the old bug this fixes was the zone rule silently erasing it.
    [Fact]
    public void ResolveRelayRules_GlobalSafetyRule_SurvivesZoneOverride_OrsInAlongside()
    {
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 1, zoneId: 5) };
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 2, isSafetyRule: true) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], zoneRules, [], [], globalRules);

        Assert.Equal([1, 2], result.Select(r => r.IDDeviceFarmUnitZoneRule).OrderBy(x => x));
    }

    [Fact]
    public void ResolveRelayRules_NonSafetyGlobalRule_StillDroppedByZoneOverride()
    {
        var zoneRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 1, zoneId: 5) };
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 2, isSafetyRule: false) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], [], zoneRules, [], [], globalRules);

        Assert.Equal([1], result.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    /// A real A/B test could otherwise turn off heating below freezing - the safety rule must OR in regardless of which tier normally wins.
    [Fact]
    public void ResolveRelayRules_GlobalSafetyRule_SurvivesExperimentOverride_OrsInAlongside()
    {
        var experimentRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 1, experimentId: 9) };
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 2, isSafetyRule: true) };

        var result = RuleHierarchyResolver.ResolveRelayRules([], experimentRules, [], [], [], globalRules);

        Assert.Equal([1, 2], result.Select(r => r.IDDeviceFarmUnitZoneRule).OrderBy(x => x));
    }

    [Fact]
    public void ResolveRelayRules_GlobalSafetyRule_SurvivesSimulationOverride_OrsInAlongside()
    {
        var simulationRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 1, simulationId: 3) };
        var globalRules = new List<DeviceFarmUnitZoneRule> { RelayRule(RelayFunction.Heating, 2, isSafetyRule: true) };

        var result = RuleHierarchyResolver.ResolveRelayRules(simulationRules, [], [], [], [], globalRules);

        Assert.Equal([1, 2], result.Select(r => r.IDDeviceFarmUnitZoneRule).OrderBy(x => x));
    }

    /// Roadmap #396(4) - Notification rules no longer group by SensorMetric (a rule can span several metrics now); a more specific scope's rule with the SAME Name replaces a less specific one instead.
    [Fact]
    public void ResolveNotificationRules_SameName_ZoneOverridesGlobal()
    {
        var zoneRule = NotificationRule("Frost Guard", 1, zoneId: 5);
        var globalRule = NotificationRule("Frost Guard", 2);

        var result = RuleHierarchyResolver.ResolveNotificationRules([], [], [zoneRule], [], [], [globalRule]);

        Assert.Equal([1], result.Select(r => r.IDDeviceFarmUnitZoneRule));
    }

    [Fact]
    public void ResolveNotificationRules_DifferentNames_AreIndependentGroups()
    {
        var zoneRule = NotificationRule("Reminder", 1, zoneId: 5);
        var globalRule = NotificationRule("Hot Alert", 2);

        var result = RuleHierarchyResolver.ResolveNotificationRules([], [], [zoneRule], [], [], [globalRule]);

        // Both survive - different names never shadow each other.
        Assert.Equal([1, 2], result.Select(r => r.IDDeviceFarmUnitZoneRule).OrderBy(x => x));
    }

    /// Roadmap #396(5) - same safety-rule survival as Relay, for Notification's name-based override.
    [Fact]
    public void ResolveNotificationRules_GlobalSafetyRule_SurvivesZoneOverride_WithSameName()
    {
        var zoneRule = NotificationRule("Frost Guard", 1, zoneId: 5);
        var globalRule = NotificationRule("Frost Guard", 2, isSafetyRule: true);

        var result = RuleHierarchyResolver.ResolveNotificationRules([], [], [zoneRule], [], [], [globalRule]);

        Assert.Equal([1, 2], result.Select(r => r.IDDeviceFarmUnitZoneRule).OrderBy(x => x));
    }
}
