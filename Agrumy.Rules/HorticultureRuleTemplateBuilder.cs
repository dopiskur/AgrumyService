using Agrumy.Shared.Models;

namespace Agrumy.Rules
{
    /// Turns one HorticultureCatalogEntry's recommended ranges into a starter set of DeviceFarmUnitZoneRule rows - a reasonable default the admin fine-tunes afterward, not a finished configuration. Only AirTemp/AirHumidity/SoilMoisture/Light convert into real rules; SoilPH/SoilEC/Co2 have no matching RelayFunction (no pH/EC dosing or CO2 injection actuator exists in this codebase) and stay informational-only on the catalog entry itself.
    public static class HorticultureRuleTemplateBuilder
    {
        // Same fixed hysteresis-per-metric convention ServerConfig's own hysteresis defaults use - a generated rule is meant to be a safe starting point, not independently tuned per catalog entry.
        private const double TempHysteresis = 1.0;
        private const double HumidityHysteresis = 5.0;
        private const double MoistureHysteresis = 5.0;
        private const double LightHysteresis = 50.0;

        public static IList<DeviceFarmUnitZoneRule> BuildRules(HorticultureCatalogEntry entry, int zoneId)
        {
            var rules = new List<DeviceFarmUnitZoneRule>();

            void Add(RelayFunction function, string suffix, SensorMetric metric, ComparisonOperator op, double value, double hysteresis) =>
                rules.Add(new DeviceFarmUnitZoneRule
                {
                    DeviceFarmUnitZoneID = zoneId,
                    ActionType = ActionType.Relay,
                    RelayFunction = function,
                    Name = $"{entry.Name}: {suffix}",
                    TargetPercent = 100,
                    Root = new ConditionNode
                    {
                        Type = NodeType.Comparison,
                        Metric = metric,
                        Operator = op,
                        Value1 = value,
                        Hysteresis = hysteresis,
                    },
                });

            if (entry.AirTempMin is double airTempMin)
            {
                Add(RelayFunction.Heating, $"heat below {airTempMin:0.#}°C", SensorMetric.Temperature, ComparisonOperator.LessThan, airTempMin, TempHysteresis);
            }
            if (entry.AirTempMax is double airTempMax)
            {
                Add(RelayFunction.Ventilation, $"vent above {airTempMax:0.#}°C", SensorMetric.Temperature, ComparisonOperator.GreaterThan, airTempMax, TempHysteresis);
            }
            if (entry.AirHumidityMax is double humidityMax)
            {
                Add(RelayFunction.Ventilation, $"vent above {humidityMax:0.#}% RH", SensorMetric.Humidity, ComparisonOperator.GreaterThan, humidityMax, HumidityHysteresis);
            }
            if (entry.SoilMoistureMin is double moistureMin)
            {
                Add(RelayFunction.WaterPump, $"water below {moistureMin:0.#}% moisture", SensorMetric.Moisture, ComparisonOperator.LessThan, moistureMin, MoistureHysteresis);
            }
            if (entry.LightMin is double lightMin)
            {
                Add(RelayFunction.Light, $"light below {lightMin:0.#} lx", SensorMetric.Light, ComparisonOperator.LessThan, lightMin, LightHysteresis);
            }

            return rules;
        }
    }
}
