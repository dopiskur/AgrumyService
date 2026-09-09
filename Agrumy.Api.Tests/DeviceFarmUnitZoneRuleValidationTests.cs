using System.ComponentModel.DataAnnotations;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests;

/// DeviceFarmUnitZoneRule.Validate() (IValidatableObject) is the DTO's own
/// self-check, with no repository/DB involved - unlike RuleValidationServiceTests (which exercises the
/// full RuleValidationService, including the DB-dependent RuleTriggered cross-reference), this proves
/// the shape/bound checks work standalone via plain System.ComponentModel.DataAnnotations.Validator, the
/// same mechanism ASP.NET Core's [ApiController] runs automatically on every model-bound request body -
/// so a malformed rule is now rejected with 400 before a controller action's own body even runs, not
/// only when that action remembers to call RuleValidationService itself.
public class DeviceFarmUnitZoneRuleValidationTests
{
    private static ConditionNode Leaf() => new() { Type = NodeType.Comparison, Metric = SensorMetric.Temperature, Operator = ComparisonOperator.GreaterThan, Value1 = 1, Hysteresis = 1 };

    private static bool IsValid(DeviceFarmUnitZoneRule rule, out List<ValidationResult> results)
    {
        results = [];
        return Validator.TryValidateObject(rule, new ValidationContext(rule), results, validateAllProperties: true);
    }

    [Fact]
    public void WellFormedRelayRule_IsValid_WithNoRepository()
    {
        var rule = new DeviceFarmUnitZoneRule { TenantID = 1, ActionType = ActionType.Relay, RelayFunction = RelayFunction.Heating, Name = "test", Root = Leaf() };

        Assert.True(IsValid(rule, out var results));
        Assert.Empty(results);
    }

    [Fact]
    public void MissingName_IsInvalid()
    {
        var rule = new DeviceFarmUnitZoneRule { TenantID = 1, ActionType = ActionType.Relay, RelayFunction = RelayFunction.Heating, Name = "", Root = Leaf() };

        Assert.False(IsValid(rule, out var results));
        Assert.Contains(results, r => r.ErrorMessage == "Name is required.");
    }

    [Fact]
    public void TreeOverHardMaxNodesPerRule_IsInvalid()
    {
        // A left-leaning chain of nested Groups, each wrapping the next plus a leaf - HardMaxNodesPerRule (8) total nodes is exceeded by nesting one group too many.
        ConditionNode root = Leaf();
        for (int i = 0; i < DeviceFarmUnitZoneRule.HardMaxNodesPerRule; i++)
        {
            root = new ConditionNode { Type = NodeType.Group, GroupOperator = LogicalOperator.And, Children = [root, Leaf()] };
        }
        var rule = new DeviceFarmUnitZoneRule { TenantID = 1, ActionType = ActionType.Relay, RelayFunction = RelayFunction.Heating, Name = "test", Root = root };

        Assert.False(IsValid(rule, out var results));
        Assert.Contains(results, r => r.ErrorMessage!.Contains("at most"));
    }

    [Fact]
    public void GroupWithZeroChildren_IsInvalid()
    {
        var rule = new DeviceFarmUnitZoneRule
        {
            TenantID = 1, ActionType = ActionType.Relay, RelayFunction = RelayFunction.Heating, Name = "test",
            Root = new ConditionNode { Type = NodeType.Group, GroupOperator = LogicalOperator.And, Children = [] },
        };

        Assert.False(IsValid(rule, out var results));
        Assert.Contains(results, r => r.ErrorMessage == "A group needs at least one child condition.");
    }

    [Fact]
    public void RuleTriggeredOnRelayRule_IsInvalid_WithNoRepository()
    {
        // The cross-reference existence check (does the referenced rule exist/match tenant) needs a DB
        // lookup and stays in RuleValidationService - but "RuleTriggered only valid on a Notification
        // rule" needs no DB access at all, so it's caught right here, standalone.
        var rule = new DeviceFarmUnitZoneRule
        {
            TenantID = 1, ActionType = ActionType.Relay, RelayFunction = RelayFunction.Heating, Name = "test",
            Root = new ConditionNode { Type = NodeType.RuleTriggered, ReferencedRuleId = 1 },
        };

        Assert.False(IsValid(rule, out var results));
        Assert.Contains(results, r => r.ErrorMessage!.Contains("another rule fired"));
    }

    [Fact]
    public void HorticultureRuleTemplateBuilderOutput_IsValid_SameCheckAsAHandAddedRule()
    {
        // Agrumy.Rules can't be referenced from here without a project reference this test project
        // doesn't have, so this mirrors HorticultureRuleTemplateBuilder.BuildRules' exact shape for one
        // entry (a single ComparisonNode leaf, Relay action, no TargetPercent) rather than importing it -
        // the point is that shape, wherever it's built, passes the same Validate() a hand-typed rule does.
        var generated = new DeviceFarmUnitZoneRule
        {
            DeviceFarmUnitZoneID = 1,
            ActionType = ActionType.Relay,
            RelayFunction = RelayFunction.Heating,
            Name = "Tomato: heat below 15.0°C",
            Root = new ConditionNode { Type = NodeType.Comparison, Metric = SensorMetric.Temperature, Operator = ComparisonOperator.LessThan, Value1 = 15.0, Hysteresis = 1.0 },
        };

        Assert.True(IsValid(generated, out var results));
        Assert.Empty(results);
    }
}
