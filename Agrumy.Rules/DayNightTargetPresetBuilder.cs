using Agrumy.Shared.Models;

namespace Agrumy.Rules
{
    /// Generates the "day AND metric&lt;dayValue" / "night AND metric&lt;nightValue" rule pair the AND-engine and Schedule node already make expressible by hand; this just saves building both trees one condition at a time. Day is always a single non-wrapping Schedule window; night is its complement, which crosses local midnight unless the day window starts at 0 or ends at 86400, so night needs an OR of up to two Schedule segments instead of one.
    public static class DayNightTargetPresetBuilder
    {
        private const int SecondsPerDay = 86400;
        private const int AllDaysOfWeek = 0b1111111;

        public static (DeviceFarmUnitZoneRule Day, DeviceFarmUnitZoneRule Night) BuildRules(
            int zoneId, RelayFunction function, SensorMetric metric, ComparisonOperator op,
            double dayValue, double nightValue, double hysteresis,
            int dayStartSeconds, int dayEndSeconds, string namePrefix)
        {
            DeviceFarmUnitZoneRule Build(string suffix, ConditionNode window, double value) => new()
            {
                DeviceFarmUnitZoneID = zoneId,
                ActionType = ActionType.Relay,
                RelayFunction = function,
                Name = $"{namePrefix}: {suffix}",
                TargetPercent = 100,
                Root = new ConditionNode
                {
                    Type = NodeType.Group,
                    GroupOperator = LogicalOperator.And,
                    Children = [window, new ConditionNode { Type = NodeType.Comparison, Metric = metric, Operator = op, Value1 = value, Hysteresis = hysteresis }],
                },
            };

            ConditionNode Schedule(int start, int duration) => new() { Type = NodeType.Schedule, DaysOfWeek = AllDaysOfWeek, Start = start, Duration = duration };

            ConditionNode dayWindow = Schedule(dayStartSeconds, dayEndSeconds - dayStartSeconds);

            var nightSegments = new List<ConditionNode>();
            if (dayEndSeconds < SecondsPerDay)
            {
                nightSegments.Add(Schedule(dayEndSeconds, SecondsPerDay - dayEndSeconds));
            }
            if (dayStartSeconds > 0)
            {
                nightSegments.Add(Schedule(0, dayStartSeconds));
            }
            ConditionNode nightWindow = nightSegments.Count == 1
                ? nightSegments[0]
                : new ConditionNode { Type = NodeType.Group, GroupOperator = LogicalOperator.Or, Children = nightSegments };

            return (Build("Day", dayWindow, dayValue), Build("Night", nightWindow, nightValue));
        }
    }
}
