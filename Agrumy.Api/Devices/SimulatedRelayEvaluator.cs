using Agrumy.Rules;
using Agrumy.Shared.Models;
using Agrumy.Api.Simulation;
using Agrumy.Shared.Utils;

namespace Agrumy.Api.Devices
{
    /// Decides a simulated device's relay on/off state from the SAME rules a real device would receive over /api/Device/Config, via RuleConditionEvaluator.EvaluateNode - the tree-walk itself is shared (roadmap #396(4) made it substantial enough that reimplementing a third time no longer made sense), only readMetric (this file) differs from RuleNotificationEvaluator's (SensorAverages-backed).
    public static class SimulatedRelayEvaluator
    {
        /// wasOn is the relay's own last-known state (for a GT/LT ComparisonNode's dead-zone latch, same rule as RuleConditionEvaluator's wasRuleTrue) - several rules for the same function OR together, unchanged from AgrumyFirmware's OR-across-rules semantics.
        public static bool Evaluate(RelayFunction function, IList<DeviceFarmUnitZoneRule> rules, bool wasOn, SimulatedReading reading, DateTime utcNow, int utcOffsetSeconds)
        {
            Func<SensorMetric, double?> readMetric = metric => ReadMetric(reading, metric);
            bool any = false;
            foreach (DeviceFarmUnitZoneRule rule in rules)
            {
                if (rule.ActionType != ActionType.Relay || rule.RelayFunction != function || rule.Root == null)
                {
                    continue;
                }
                any |= RuleConditionEvaluator.EvaluateRule(rule, wasOn, readMetric, utcNow, utcOffsetSeconds, referencedRuleFiredThisTick: static _ => false);
            }
            return any;
        }

        /// Positional counterpart to Evaluate (Screen/Vent) - folds every currently-true rule's own TargetPercent to their MAX (0 if none true), same "OR across rules" spirit as Evaluate just MAX instead of boolean-OR, mirroring AgrumyFirmware's foldTargetPercent.
        public static int EvaluatePercent(RelayFunction function, IList<DeviceFarmUnitZoneRule> rules, bool wasOn, SimulatedReading reading, DateTime utcNow, int utcOffsetSeconds)
        {
            Func<SensorMetric, double?> readMetric = metric => ReadMetric(reading, metric);
            int best = 0;
            foreach (DeviceFarmUnitZoneRule rule in rules)
            {
                if (rule.ActionType != ActionType.Relay || rule.RelayFunction != function || rule.Root == null || rule.TargetPercent is not int percent)
                {
                    continue;
                }
                if (RuleConditionEvaluator.EvaluateRule(rule, wasOn, readMetric, utcNow, utcOffsetSeconds, referencedRuleFiredThisTick: static _ => false) && percent > best)
                {
                    best = percent;
                }
            }
            return best;
        }

        private static double? ReadMetric(SimulatedReading r, SensorMetric metric) => metric switch
        {
            SensorMetric.Temperature => r.Temperature,
            SensorMetric.SoilTemperature => r.SoilTemperature,
            SensorMetric.Humidity => r.Humidity,
            SensorMetric.Vpd => VpdCalculator.Compute(r.Temperature, r.Humidity),
            SensorMetric.DewPoint => DewPointCalculator.Compute(r.Temperature, r.Humidity),
            SensorMetric.DewPointSpread => DewPointCalculator.Compute(r.Temperature, r.Humidity) is double dp ? r.Temperature - dp : null,
            SensorMetric.Moisture => r.Moisture,
            SensorMetric.Light => r.Light,
            SensorMetric.Co2 => r.Co2,
            SensorMetric.Tvoc => r.Tvoc,
            SensorMetric.Barometer => r.Barometer,
            SensorMetric.LiquidPH => r.LiquidPH,
            SensorMetric.RainLevel => r.RainLevel,
            SensorMetric.WaterLevel => r.WaterLevel,
            SensorMetric.Wind => r.Wind,
            SensorMetric.Ec => r.Ec,
            SensorMetric.Weight => r.Weight,
            _ => null,
        };
    }
}
