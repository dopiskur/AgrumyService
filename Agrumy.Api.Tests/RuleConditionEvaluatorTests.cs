using api.Devices;
using api.Models;

namespace Agrumy.Api.Tests;

/// Server-side Notification-action rule evaluation (roadmap #396(4)) - mirrors AgrumyFirmware's RelayLogic.cpp semantics for the same node types, over the recursive ConditionNode tree.
public class RuleConditionEvaluatorTests
{
    private static DeviceFarmUnitZoneRule Rule(ConditionNode root) => new()
    {
        IDDeviceFarmUnitZoneRule = 1,
        TenantID = 1,
        ActionType = ActionType.Notification,
        Name = "test",
        Root = root,
        NotificationSubject = "test",
    };

    private static ConditionNode Comparison(ComparisonOperator op, double value1, double? value2 = null, double? hysteresis = null) =>
        new() { Type = NodeType.Comparison, Metric = SensorMetric.Temperature, Operator = op, Value1 = value1, Value2 = value2, Hysteresis = hysteresis };

    private static ConditionNode Group(LogicalOperator op, params ConditionNode[] children) =>
        new() { Type = NodeType.Group, GroupOperator = op, Children = children };

    private static Func<SensorMetric, double?> Reading(double? value) => _ => value;

    [Fact]
    public void GreaterThan_ReadingAboveThreshold_TurnsOn()
    {
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(Comparison(ComparisonOperator.GreaterThan, 30, hysteresis: 2)), wasRuleTrue: false, Reading(33), DateTime.UtcNow, 0, _ => false);
        Assert.True(result);
    }

    [Fact]
    public void GreaterThan_ReadingAtThreshold_StaysOff()
    {
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(Comparison(ComparisonOperator.GreaterThan, 30, hysteresis: 2)), wasRuleTrue: false, Reading(30), DateTime.UtcNow, 0, _ => false);
        Assert.False(result);
    }

    [Fact]
    public void GreaterThan_DeadZone_LatchesOnPreviousRuleState()
    {
        // Dead zone sits BELOW threshold(30): (threshold-hysteresis, threshold] = (28, 30]. wasRuleTrue is the ONLY state available (no per-node storage), so it drives the latch here.
        bool stillOn = RuleConditionEvaluator.EvaluateRule(Rule(Comparison(ComparisonOperator.GreaterThan, 30, hysteresis: 2)), wasRuleTrue: true, Reading(29), DateTime.UtcNow, 0, _ => false);
        bool staysOff = RuleConditionEvaluator.EvaluateRule(Rule(Comparison(ComparisonOperator.GreaterThan, 30, hysteresis: 2)), wasRuleTrue: false, Reading(29), DateTime.UtcNow, 0, _ => false);
        Assert.True(stillOn);
        Assert.False(staysOff);
    }

    [Fact]
    public void Comparison_NoReading_IsFalse()
    {
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(Comparison(ComparisonOperator.GreaterThan, 30, hysteresis: 2)), wasRuleTrue: true, Reading(null), DateTime.UtcNow, 0, _ => false);
        Assert.False(result);
    }

    [Fact]
    public void GreaterThanOrEqual_AtBoundary_IsTrue()
    {
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(Comparison(ComparisonOperator.GreaterThanOrEqual, 30)), wasRuleTrue: false, Reading(30), DateTime.UtcNow, 0, _ => false);
        Assert.True(result);
    }

    [Fact]
    public void Between_Inclusive_BothBoundsMatch()
    {
        var node = Comparison(ComparisonOperator.Between, 20, 60);
        Assert.True(RuleConditionEvaluator.EvaluateRule(Rule(node), false, Reading(20), DateTime.UtcNow, 0, _ => false));
        Assert.True(RuleConditionEvaluator.EvaluateRule(Rule(node), false, Reading(60), DateTime.UtcNow, 0, _ => false));
        Assert.False(RuleConditionEvaluator.EvaluateRule(Rule(node), false, Reading(60.01), DateTime.UtcNow, 0, _ => false));
    }

    [Fact]
    public void Interval_WithinOnWindow_IsTrue()
    {
        var node = new ConditionNode { Type = NodeType.Interval, Interval = 3600, IntervalLength = 60 };
        // Epoch 0 (1970-01-01T00:00:00Z) is grid-aligned to the start of every interval - position-in-cycle 0, within the first 60s.
        var epochZero = DateTimeOffset.FromUnixTimeSeconds(0).UtcDateTime;
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(node), wasRuleTrue: false, Reading(null), epochZero, 0, _ => false);
        Assert.True(result);
    }

    [Fact]
    public void Interval_OutsideOnWindow_IsFalse()
    {
        var node = new ConditionNode { Type = NodeType.Interval, Interval = 3600, IntervalLength = 60 };
        var midCycle = DateTimeOffset.FromUnixTimeSeconds(1800).UtcDateTime; // 30 minutes into a 60-minute cycle, well past the 60s on-window
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(node), wasRuleTrue: false, Reading(null), midCycle, 0, _ => false);
        Assert.False(result);
    }

    [Fact]
    public void Schedule_WithinWindowOnScheduledDay_IsTrue()
    {
        // 2026-09-06 is a Sunday (bit 0). Window 08:00-09:00 local, checked at 08:30 UTC with 0 offset.
        var node = new ConditionNode { Type = NodeType.Schedule, DaysOfWeek = 0b1, Start = 8 * 3600, Duration = 3600 };
        var sundayMorning = new DateTime(2026, 9, 6, 8, 30, 0, DateTimeKind.Utc);
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(node), wasRuleTrue: false, Reading(null), sundayMorning, 0, _ => false);
        Assert.True(result);
    }

    [Fact]
    public void Schedule_WrongDay_IsFalse()
    {
        var node = new ConditionNode { Type = NodeType.Schedule, DaysOfWeek = 0b1, Start = 8 * 3600, Duration = 3600 }; // Sunday only
        var mondayMorning = new DateTime(2026, 9, 7, 8, 30, 0, DateTimeKind.Utc);
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(node), wasRuleTrue: false, Reading(null), mondayMorning, 0, _ => false);
        Assert.False(result);
    }

    [Fact]
    public void RuleTriggered_ReferencedRuleFiredThisTick_IsTrue()
    {
        var node = new ConditionNode { Type = NodeType.RuleTriggered, ReferencedRuleId = 42 };
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(node), wasRuleTrue: false, Reading(null), DateTime.UtcNow, 0, id => id == 42);
        Assert.True(result);
    }

    [Fact]
    public void RuleTriggered_ReferencedRuleDidNotFire_IsFalse()
    {
        var node = new ConditionNode { Type = NodeType.RuleTriggered, ReferencedRuleId = 42 };
        bool result = RuleConditionEvaluator.EvaluateRule(Rule(node), wasRuleTrue: false, Reading(null), DateTime.UtcNow, 0, id => id == 99);
        Assert.False(result);
    }

    [Fact]
    public void Group_ThreeConditions_MixedAndOr_NeedsExplicitNesting()
    {
        // (false AND true) OR true = true - a precedence-aware evaluator (AND binds tighter) would instead compute false AND (true OR true) = false.
        // Mixed operators now need an explicit inner group (roadmap #396(4)) - unlike the old flat left-to-right fold where this was implicit.
        var rule = Rule(Group(LogicalOperator.Or,
            Group(LogicalOperator.And, Comparison(ComparisonOperator.GreaterThan, 1000), Comparison(ComparisonOperator.GreaterThan, -1000)), // false AND true = false
            Comparison(ComparisonOperator.GreaterThan, -1000))); // true
        bool result = RuleConditionEvaluator.EvaluateRule(rule, wasRuleTrue: false, Reading(5), DateTime.UtcNow, 0, _ => false);
        Assert.True(result);
    }

    [Fact]
    public void FindFirstComparison_WalksIntoGroup()
    {
        var node = Group(LogicalOperator.And, new ConditionNode { Type = NodeType.Interval, Interval = 60, IntervalLength = 10 }, Comparison(ComparisonOperator.Equal, 42));
        ConditionNode? found = RuleConditionEvaluator.FindFirstComparison(node);
        Assert.NotNull(found);
        Assert.Equal(42, found!.Value1);
    }
}
