using Agrumy.Rules;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests;

public class HorticultureRuleTemplateBuilderTests
{
    [Fact]
    public void AllRangesSet_GeneratesOneRulePerRange()
    {
        var entry = new HorticultureCatalogEntry
        {
            Name = "Tomato",
            AirTempMin = 18, AirTempMax = 28,
            AirHumidityMax = 70,
            SoilMoistureMin = 40,
            LightMin = 5000,
            // Informational-only fields - must NOT produce rules.
            SoilPHMin = 5.5, SoilPHMax = 6.5,
            SoilECMin = 1.5, SoilECMax = 2.5,
            Co2Min = 800, Co2Max = 1200,
        };

        var rules = HorticultureRuleTemplateBuilder.BuildRules(entry, zoneId: 42);

        Assert.Equal(5, rules.Count);
        Assert.All(rules, r => Assert.Equal(42, r.DeviceFarmUnitZoneID));
        Assert.All(rules, r => Assert.Equal(ActionType.Relay, r.ActionType));
        Assert.All(rules, r => Assert.StartsWith("Tomato:", r.Name));

        Assert.Contains(rules, r => r.RelayFunction == RelayFunction.Heating && r.Root!.Metric == SensorMetric.Temperature && r.Root.Operator == ComparisonOperator.LessThan && r.Root.Value1 == 18);
        Assert.Contains(rules, r => r.RelayFunction == RelayFunction.Ventilation && r.Root!.Metric == SensorMetric.Temperature && r.Root.Operator == ComparisonOperator.GreaterThan && r.Root.Value1 == 28);
        Assert.Contains(rules, r => r.RelayFunction == RelayFunction.Ventilation && r.Root!.Metric == SensorMetric.Humidity && r.Root.Operator == ComparisonOperator.GreaterThan && r.Root.Value1 == 70);
        Assert.Contains(rules, r => r.RelayFunction == RelayFunction.WaterPump && r.Root!.Metric == SensorMetric.Moisture && r.Root.Operator == ComparisonOperator.LessThan && r.Root.Value1 == 40);
        Assert.Contains(rules, r => r.RelayFunction == RelayFunction.Light && r.Root!.Metric == SensorMetric.Light && r.Root.Operator == ComparisonOperator.LessThan && r.Root.Value1 == 5000);
    }

    [Fact]
    public void NoRangesSet_GeneratesNoRules()
    {
        var entry = new HorticultureCatalogEntry { Name = "Unspecified" };

        var rules = HorticultureRuleTemplateBuilder.BuildRules(entry, zoneId: 1);

        Assert.Empty(rules);
    }

    [Fact]
    public void OnlyAirTempMinSet_GeneratesExactlyOneRule()
    {
        var entry = new HorticultureCatalogEntry { Name = "Cold-hardy crop", AirTempMin = 5 };

        var rules = HorticultureRuleTemplateBuilder.BuildRules(entry, zoneId: 7);

        var rule = Assert.Single(rules);
        Assert.Equal(RelayFunction.Heating, rule.RelayFunction);
        Assert.Equal(7, rule.DeviceFarmUnitZoneID);
    }
}
