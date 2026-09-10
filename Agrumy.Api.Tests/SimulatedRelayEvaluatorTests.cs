using Agrumy.Api.Devices;
using Agrumy.Shared.Models;
using Agrumy.Api.Simulation;

namespace Agrumy.Api.Tests;

/// Relay evaluation for a simulated device against the SAME rule shape a real device receives over /api/Device/Config (metric is now explicit per ComparisonNode, no longer implied by the rule's RelayFunction; every function now folds through TargetPercent/MAX, not just Screen/Vent).
public class SimulatedRelayEvaluatorTests
{
    private static DeviceFarmUnitZoneRule Rule(RelayFunction function, ConditionNode root, int targetPercent = 100) => new()
    {
        IDDeviceFarmUnitZoneRule = 1,
        TenantID = 1,
        ActionType = ActionType.Relay,
        RelayFunction = function,
        TargetPercent = targetPercent,
        Name = "test",
        Root = root,
    };

    private static ConditionNode Comparison(SensorMetric metric, ComparisonOperator op, double value1, double hysteresis = 0) =>
        new() { Type = NodeType.Comparison, Metric = metric, Operator = op, Value1 = value1, Hysteresis = hysteresis };

    private static SimulatedReading Reading(double humidity = 50, double temperature = 20, int light = 5000, int waterLevel = 50) => new()
    {
        Humidity = humidity,
        Temperature = temperature,
        Light = light,
        WaterLevel = waterLevel,
    };

    [Fact]
    public void Ventilation_TurnsOnAboveHumidityThreshold()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { Rule(RelayFunction.Ventilation, Comparison(SensorMetric.Humidity, ComparisonOperator.GreaterThan, 60, hysteresis: 2)) };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Ventilation, rules, wasOn: false, Reading(humidity: 65), DateTime.UtcNow, 0);
        Assert.Equal(100, percent);
    }

    [Fact]
    public void Heating_TurnsOnBelowTemperatureThreshold()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { Rule(RelayFunction.Heating, Comparison(SensorMetric.Temperature, ComparisonOperator.LessThan, 18, hysteresis: 1)) };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Heating, rules, wasOn: false, Reading(temperature: 15), DateTime.UtcNow, 0);
        Assert.Equal(100, percent);
    }

    [Fact]
    public void Heating_AboveThreshold_StaysOff()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { Rule(RelayFunction.Heating, Comparison(SensorMetric.Temperature, ComparisonOperator.LessThan, 18, hysteresis: 1)) };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Heating, rules, wasOn: false, Reading(temperature: 22), DateTime.UtcNow, 0);
        Assert.Equal(0, percent);
    }

    [Fact]
    public void SeveralRulesForSameFunction_OnlyMatchingFunctionCounted()
    {
        var rules = new List<DeviceFarmUnitZoneRule>
        {
            Rule(RelayFunction.Light, Comparison(SensorMetric.Light, ComparisonOperator.LessThan, 1000, hysteresis: 50)), // below 1000 -> on; reading is 5000, so this alone is off
            Rule(RelayFunction.WaterPump, Comparison(SensorMetric.WaterLevel, ComparisonOperator.LessThan, 30, hysteresis: 2)), // different function - must not affect Light's result
        };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Light, rules, wasOn: false, Reading(light: 5000), DateTime.UtcNow, 0);
        Assert.Equal(0, percent);
    }

    // A binary function (Heating) with two staged, simultaneously-true rules now MAX-folds too, not OR - same fold every function shares now.
    [Fact]
    public void SeveralRulesForSameFunction_HighestTargetPercentWins()
    {
        var rules = new List<DeviceFarmUnitZoneRule>
        {
            Rule(RelayFunction.Heating, Comparison(SensorMetric.Temperature, ComparisonOperator.LessThan, 20, hysteresis: 1), targetPercent: 40),
            Rule(RelayFunction.Heating, Comparison(SensorMetric.Temperature, ComparisonOperator.LessThan, 10, hysteresis: 1), targetPercent: 100),
        };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Heating, rules, wasOn: false, Reading(temperature: 5), DateTime.UtcNow, 0);
        Assert.Equal(100, percent);
    }

    [Fact]
    public void UnrelatedFunction_WithNoMatchingRule_IsFalse()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { Rule(RelayFunction.Heating, Comparison(SensorMetric.Temperature, ComparisonOperator.LessThan, 18, hysteresis: 1)) };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.WaterPump, rules, wasOn: true, Reading(waterLevel: 10), DateTime.UtcNow, 0);
        Assert.Equal(0, percent);
    }

    [Fact]
    public void DerivedMetric_DewPointSpread_ReadableInSimulation()
    {
        // Roadmap #396(4) - a Comparison node can now read a DERIVED metric too, computed on-the-fly from temperature+humidity.
        var rules = new List<DeviceFarmUnitZoneRule> { Rule(RelayFunction.Ventilation, Comparison(SensorMetric.DewPointSpread, ComparisonOperator.LessThan, 3)) };
        // High humidity + moderate temp narrows the spread well below 3.
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Ventilation, rules, wasOn: false, Reading(humidity: 95, temperature: 20), DateTime.UtcNow, 0);
        Assert.Equal(100, percent);
    }

    [Fact]
    public void EvaluatePercent_NoRuleTrue_ReturnsZero()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { Rule(RelayFunction.Vent, Comparison(SensorMetric.Temperature, ComparisonOperator.GreaterThan, 30, hysteresis: 1), targetPercent: 50) };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Vent, rules, wasOn: false, Reading(temperature: 20), DateTime.UtcNow, 0);
        Assert.Equal(0, percent);
    }

    [Fact]
    public void EvaluatePercent_OneRuleTrue_ReturnsItsTargetPercent()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { Rule(RelayFunction.Vent, Comparison(SensorMetric.Temperature, ComparisonOperator.GreaterThan, 25, hysteresis: 1), targetPercent: 30) };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Vent, rules, wasOn: false, Reading(temperature: 28), DateTime.UtcNow, 0);
        Assert.Equal(30, percent);
    }

    // Two threshold rules for the same function both true (temperature past both points) - the higher-demanding one wins, same reasoning as AgrumyFirmware's foldTargetPercent.
    [Fact]
    public void EvaluatePercent_TwoRulesTrue_HighestTargetPercentWins()
    {
        var rules = new List<DeviceFarmUnitZoneRule>
        {
            Rule(RelayFunction.Vent, Comparison(SensorMetric.Temperature, ComparisonOperator.GreaterThan, 25, hysteresis: 1), targetPercent: 30),
            Rule(RelayFunction.Vent, Comparison(SensorMetric.Temperature, ComparisonOperator.GreaterThan, 30, hysteresis: 1), targetPercent: 70),
        };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Vent, rules, wasOn: false, Reading(temperature: 35), DateTime.UtcNow, 0);
        Assert.Equal(70, percent);
    }

    [Fact]
    public void EvaluatePercent_DifferentFunction_Ignored()
    {
        var rules = new List<DeviceFarmUnitZoneRule> { Rule(RelayFunction.Vent, Comparison(SensorMetric.Temperature, ComparisonOperator.GreaterThan, 20, hysteresis: 1), targetPercent: 80) };
        int percent = SimulatedRelayEvaluator.EvaluatePercent(RelayFunction.Screen, rules, wasOn: false, Reading(temperature: 30), DateTime.UtcNow, 0);
        Assert.Equal(0, percent);
    }
}
