using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Devices;
using Agrumy.Shared.Models;
using Moq;

namespace Agrumy.Api.Tests;

/// TargetPercent shape validation - required+bounded (0-100) for every Relay function, forbidden for a Notification rule.
public class RuleValidationServiceTests
{
    private readonly RuleValidationService _sut = new(new Mock<IDeviceFarmUnitRepository>().Object);

    private static ConditionNode Leaf() => new() { Type = NodeType.Comparison, Metric = SensorMetric.Temperature, Operator = ComparisonOperator.GreaterThan, Value1 = 1, Hysteresis = 1 };

    private static DeviceFarmUnitZoneRule RelayRule(RelayFunction function, int? targetPercent) => new()
    {
        TenantID = 1,
        ActionType = ActionType.Relay,
        RelayFunction = function,
        TargetPercent = targetPercent,
        Name = "test",
        Root = Leaf(),
    };

    [Fact]
    public async Task PositionalFunction_MissingTargetPercent_Rejected()
    {
        string? error = await _sut.ShapeErrorAsync(RelayRule(RelayFunction.Vent, null));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task PositionalFunction_TargetPercentOutOfRange_Rejected(int percent)
    {
        string? error = await _sut.ShapeErrorAsync(RelayRule(RelayFunction.Screen, percent));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task PositionalFunction_TargetPercentInRange_Accepted(int percent)
    {
        string? error = await _sut.ShapeErrorAsync(RelayRule(RelayFunction.Vent, percent));
        Assert.Null(error);
    }

    [Fact]
    public async Task BinaryFunction_TargetPercentSet_Accepted()
    {
        string? error = await _sut.ShapeErrorAsync(RelayRule(RelayFunction.Heating, 50));
        Assert.Null(error);
    }

    [Fact]
    public async Task BinaryFunction_NoTargetPercent_Rejected()
    {
        string? error = await _sut.ShapeErrorAsync(RelayRule(RelayFunction.Heating, null));
        Assert.NotNull(error);
    }

    [Fact]
    public async Task NotificationRule_TargetPercentSet_Rejected()
    {
        var rule = new DeviceFarmUnitZoneRule
        {
            TenantID = 1,
            ActionType = ActionType.Notification,
            TargetPercent = 50,
            Name = "test",
            NotificationSubject = "subject",
            Root = Leaf(),
        };
        string? error = await _sut.ShapeErrorAsync(rule);
        Assert.NotNull(error);
    }
}
