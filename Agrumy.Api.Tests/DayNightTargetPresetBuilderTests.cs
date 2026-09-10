using System.ComponentModel.DataAnnotations;
using Agrumy.Rules;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests;

public class DayNightTargetPresetBuilderTests
{
    [Fact]
    public void MidDayWindow_NightWraps_GeneratesTwoValidRules()
    {
        (DeviceFarmUnitZoneRule day, DeviceFarmUnitZoneRule night) = DayNightTargetPresetBuilder.BuildRules(
            zoneId: 42, RelayFunction.Heating, SensorMetric.Temperature, ComparisonOperator.LessThan,
            dayValue: 24, nightValue: 18, hysteresis: 1, dayStartSeconds: 6 * 3600, dayEndSeconds: 20 * 3600, namePrefix: "Vegetative");

        Assert.Equal("Vegetative: Day", day.Name);
        Assert.Equal("Vegetative: Night", night.Name);
        Assert.Equal(42, day.DeviceFarmUnitZoneID);
        Assert.Equal(42, night.DeviceFarmUnitZoneID);
        Assert.Equal(RelayFunction.Heating, day.RelayFunction);
        Assert.Equal(RelayFunction.Heating, night.RelayFunction);

        // Day: single non-wrapping Schedule window AND the day threshold.
        Assert.Equal(NodeType.Group, day.Root!.Type);
        Assert.Equal(LogicalOperator.And, day.Root.GroupOperator);
        ConditionNode daySchedule = Assert.Single(day.Root.Children, c => c.Type == NodeType.Schedule);
        Assert.Equal(6 * 3600, daySchedule.Start);
        Assert.Equal(14 * 3600, daySchedule.Duration);
        ConditionNode dayCompare = Assert.Single(day.Root.Children, c => c.Type == NodeType.Comparison);
        Assert.Equal(24, dayCompare.Value1);

        // Night: OR of the two wrap-around Schedule segments (20:00-24:00, 00:00-06:00) AND the night threshold.
        Assert.Equal(NodeType.Group, night.Root!.Type);
        Assert.Equal(LogicalOperator.And, night.Root.GroupOperator);
        ConditionNode nightWindow = Assert.Single(night.Root.Children, c => c.Type == NodeType.Group);
        Assert.Equal(LogicalOperator.Or, nightWindow.GroupOperator);
        Assert.Equal(2, nightWindow.Children.Count);
        Assert.Contains(nightWindow.Children, s => s.Start == 20 * 3600 && s.Duration == 4 * 3600);
        Assert.Contains(nightWindow.Children, s => s.Start == 0 && s.Duration == 6 * 3600);
        ConditionNode nightCompare = Assert.Single(night.Root.Children, c => c.Type == NodeType.Comparison);
        Assert.Equal(18, nightCompare.Value1);

        // Both trees must pass the same shape/bound validation a hand-built rule would (node/child caps, Schedule bounds).
        Assert.Empty(day.Validate(new ValidationContext(day)));
        Assert.Empty(night.Validate(new ValidationContext(night)));
    }

    [Fact]
    public void DayStartsAtMidnight_NightIsSingleSegment()
    {
        (_, DeviceFarmUnitZoneRule night) = DayNightTargetPresetBuilder.BuildRules(
            zoneId: 1, RelayFunction.Light, SensorMetric.Light, ComparisonOperator.LessThan,
            dayValue: 5000, nightValue: 0, hysteresis: 0, dayStartSeconds: 0, dayEndSeconds: 18 * 3600, namePrefix: "Preset");

        ConditionNode nightWindow = Assert.Single(night.Root!.Children, c => c.Type is NodeType.Schedule or NodeType.Group);
        Assert.Equal(NodeType.Schedule, nightWindow.Type);
        Assert.Equal(18 * 3600, nightWindow.Start);
        Assert.Equal(6 * 3600, nightWindow.Duration);
    }

    [Fact]
    public void DayEndsAtMidnight_NightIsSingleSegment()
    {
        (_, DeviceFarmUnitZoneRule night) = DayNightTargetPresetBuilder.BuildRules(
            zoneId: 1, RelayFunction.Light, SensorMetric.Light, ComparisonOperator.LessThan,
            dayValue: 5000, nightValue: 0, hysteresis: 0, dayStartSeconds: 6 * 3600, dayEndSeconds: 86400, namePrefix: "Preset");

        ConditionNode nightWindow = Assert.Single(night.Root!.Children, c => c.Type is NodeType.Schedule or NodeType.Group);
        Assert.Equal(NodeType.Schedule, nightWindow.Type);
        Assert.Equal(0, nightWindow.Start);
        Assert.Equal(6 * 3600, nightWindow.Duration);
    }
}
