using System.Linq;
using api.Models;

namespace api.Devices
{
    /// Server-side evaluation of one rule's ConditionNode tree (roadmap #396(4)), mirroring AgrumyFirmware's
    /// RelayLogic.cpp/ActuatorController semantics exactly (same GT/LT dead-zone math, same grid-aligned
    /// Interval formula, same Schedule window check, same recursive AND/OR fold) - reimplemented in C#
    /// rather than shared, since firmware runs a genuinely separate language/runtime. Used directly by
    /// Notification-action rules (server-side); api.Devices.SimulatedRelayEvaluator reuses EvaluateNode
    /// for simulated Relay-action rules too, passing its own readMetric - the tree-walk itself doesn't
    /// need reimplementing a second time now that it's this much more than a few lines.
    public static class RuleConditionEvaluator
    {
        /// wasRuleTrue is the rule's own last-known folded result - used as EVERY GT/LT ComparisonNode's
        /// dead-zone latch input, since there is no per-node state to store. An approximation for a rule
        /// with more than one GT/LT node (each shares the whole rule's latch rather than its own), traded
        /// for not needing a per-node state table.
        public static bool EvaluateRule(DeviceFarmUnitZoneRule rule, bool wasRuleTrue, Func<SensorMetric, double?> readMetric,
            DateTime utcNow, int utcOffsetSeconds, Func<int, bool> referencedRuleFiredThisTick, SensorTrend? trend = null) =>
            rule.Root != null && EvaluateNode(rule.Root, wasRuleTrue, readMetric, utcNow, utcOffsetSeconds, referencedRuleFiredThisTick, trend);

        public static bool EvaluateNode(ConditionNode node, bool wasRuleTrue, Func<SensorMetric, double?> readMetric,
            DateTime utcNow, int utcOffsetSeconds, Func<int, bool> referencedRuleFiredThisTick, SensorTrend? trend = null)
        {
            switch (node.Type)
            {
                case NodeType.Comparison:
                {
                    if (node.Metric is not SensorMetric metric || node.Operator is not ComparisonOperator op || node.Value1 is not double value1)
                    {
                        return false;
                    }
                    double? reading = readMetric(metric);
                    if (reading is not double value || double.IsNaN(value))
                    {
                        return false;
                    }
                    return op switch
                    {
                        ComparisonOperator.GreaterThan => ComputeThresholdState(wasRuleTrue, value, value1, node.Hysteresis ?? 0, turnsOnAboveThreshold: true),
                        ComparisonOperator.LessThan => ComputeThresholdState(wasRuleTrue, value, value1, node.Hysteresis ?? 0, turnsOnAboveThreshold: false),
                        ComparisonOperator.GreaterThanOrEqual => value >= value1,
                        ComparisonOperator.LessThanOrEqual => value <= value1,
                        ComparisonOperator.Equal => value == value1,
                        ComparisonOperator.Between => node.Value2 is double value2 && value >= Math.Min(value1, value2) && value <= Math.Max(value1, value2),
                        _ => false,
                    };
                }
                case NodeType.Interval:
                    return node.Interval is int interval && interval > 0
                        && ComputeIntervalState(interval, node.IntervalLength ?? 0, utcNow);
                case NodeType.Schedule:
                {
                    if (node.DaysOfWeek is not int daysOfWeek || node.Start is not int start || node.Duration is not int duration)
                    {
                        return false;
                    }
                    DateTime local = utcNow.AddSeconds(utcOffsetSeconds);
                    int localWeekday = (int)local.DayOfWeek; // 0=Sunday..6=Saturday, matches the 7-bit mask convention
                    int localSecondsOfDay = local.Hour * 3600 + local.Minute * 60 + local.Second;
                    return ComputeScheduleState(daysOfWeek, start, duration, localWeekday, localSecondsOfDay);
                }
                case NodeType.RuleTriggered:
                    return node.ReferencedRuleId is int referencedRuleId && referencedRuleFiredThisTick(referencedRuleId);
                case NodeType.RateOfChange:
                {
                    // Only meaningful server-side (roadmap #398(1)) - validation restricts this to Notification rules, so trend is always non-null by the time it matters; a null trend (Relay/simulated path) just evaluates false, same fail-closed shape as a missing reading elsewhere in this switch.
                    if (trend == null || node.Metric is not SensorMetric metric || node.WindowHours is not int windowHours
                        || windowHours is < 1 or >= SensorTrend.HourBuckets || node.ChangeThreshold is not double changeThreshold)
                    {
                        return false;
                    }
                    double? current = readMetric(metric);
                    double? past = TrendBucket(trend, metric, SensorTrend.HourBuckets - 1 - windowHours);
                    return current is double c && !double.IsNaN(c) && past is double p && Math.Abs(c - p) >= changeThreshold;
                }
                case NodeType.DifDisruption:
                {
                    // Roadmap #398(3) - "day" and "night" here are just the two windows relative to now, not calendar/sunrise-aligned; always Temperature, no per-node Metric.
                    if (trend == null || node.NightWindowHours is not int nightHours || node.DayWindowHours is not int dayHours
                        || nightHours < 1 || dayHours < 1 || nightHours + dayHours > SensorTrend.HourBuckets || node.MinDifDegrees is not double minDif)
                    {
                        return false;
                    }
                    double? nightAvg = TrendBucketAverage(trend.Temperature, SensorTrend.HourBuckets - nightHours, SensorTrend.HourBuckets - 1);
                    double? dayAvg = TrendBucketAverage(trend.Temperature, SensorTrend.HourBuckets - nightHours - dayHours, SensorTrend.HourBuckets - nightHours - 1);
                    return nightAvg is double n && dayAvg is double d && (d - n) < minDif;
                }
                case NodeType.Group:
                {
                    if (node.Children.Count == 0)
                    {
                        return false;
                    }
                    bool result = EvaluateNode(node.Children[0], wasRuleTrue, readMetric, utcNow, utcOffsetSeconds, referencedRuleFiredThisTick, trend);
                    for (int i = 1; i < node.Children.Count; i++)
                    {
                        bool next = EvaluateNode(node.Children[i], wasRuleTrue, readMetric, utcNow, utcOffsetSeconds, referencedRuleFiredThisTick, trend);
                        result = node.GroupOperator == LogicalOperator.And ? (result && next) : (result || next);
                    }
                    return result;
                }
                case NodeType.Astronomical:
                default:
                    // Astronomical never reaches evaluation as-is on either path (roadmap #398(2) extended AstronomicalRuleResolver.Resolve to Notification rules too, not just Relay) - it's always compiled into an effective Schedule node first.
                    return false;
            }
        }

        private static double? TrendBucket(SensorTrend trend, SensorMetric metric, int bucketIndex) => metric switch
        {
            SensorMetric.Temperature => trend.Temperature[bucketIndex],
            SensorMetric.SoilTemperature => trend.SoilTemperature[bucketIndex],
            SensorMetric.Humidity => trend.Humidity[bucketIndex],
            SensorMetric.Vpd => trend.Vpd[bucketIndex],
            SensorMetric.DewPoint => trend.DewPoint[bucketIndex],
            SensorMetric.DewPointSpread => trend.DewPointSpread[bucketIndex],
            SensorMetric.Moisture => trend.Moisture[bucketIndex],
            SensorMetric.Light => trend.Light[bucketIndex],
            SensorMetric.Co2 => trend.Co2[bucketIndex],
            SensorMetric.Tvoc => trend.Tvoc[bucketIndex],
            SensorMetric.Barometer => trend.Barometer[bucketIndex],
            SensorMetric.LiquidPH => trend.LiquidPH[bucketIndex],
            SensorMetric.RainLevel => trend.RainLevel[bucketIndex],
            SensorMetric.WaterLevel => trend.WaterLevel[bucketIndex],
            SensorMetric.Wind => trend.Wind[bucketIndex],
            SensorMetric.Ec => trend.Ec[bucketIndex],
            SensorMetric.Weight => trend.Weight[bucketIndex],
            _ => null,
        };

        /// Plain mean of the inclusive [fromBucket, toBucket] range, ignoring null buckets; null if every bucket in range is null (not enough history yet).
        private static double? TrendBucketAverage(double?[] buckets, int fromBucket, int toBucket)
        {
            var values = buckets.Skip(fromBucket).Take(toBucket - fromBucket + 1).Where(v => v != null).Select(v => v!.Value).ToList();
            return values.Count > 0 ? values.Average() : null;
        }

        // ---- Pure math, mirrors AgrumyFirmware's RelayLogic.cpp exactly. ---------------------------------

        internal static bool ComputeThresholdState(bool currentlyOn, double reading, double threshold, double hysteresis, bool turnsOnAboveThreshold)
        {
            bool shouldTurnOn = turnsOnAboveThreshold ? reading > threshold : reading < threshold;
            bool shouldTurnOff = turnsOnAboveThreshold ? reading <= threshold - hysteresis : reading >= threshold + hysteresis;
            if (!currentlyOn && shouldTurnOn) { return true; }
            if (currentlyOn && shouldTurnOff) { return false; }
            return currentlyOn; // dead zone - neither condition met, state latches
        }

        internal static bool ComputeIntervalState(int interval, int intervalLength, DateTime utcNow)
        {
            long epochSeconds = ((DateTimeOffset)DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)).ToUnixTimeSeconds();
            long positionInCycle = epochSeconds % interval;
            return positionInCycle < intervalLength;
        }

        internal static bool ComputeScheduleState(int daysOfWeekMask, int startSeconds, int durationSeconds, int localWeekday, int localSecondsOfDay)
        {
            bool todayIsScheduled = (daysOfWeekMask & (1 << localWeekday)) != 0;
            return todayIsScheduled && localSecondsOfDay >= startSeconds && localSecondsOfDay < startSeconds + durationSeconds;
        }

        /// First ComparisonNode found in a depth-first walk of the tree, or null - best-effort source for the {metric}/{value} notification placeholders now that a rule can span several metrics (RuleNotificationEvaluator).
        public static ConditionNode? FindFirstComparison(ConditionNode? node)
        {
            if (node == null)
            {
                return null;
            }
            if (node.Type == NodeType.Comparison)
            {
                return node;
            }
            if (node.Type == NodeType.Group)
            {
                foreach (ConditionNode child in node.Children)
                {
                    if (FindFirstComparison(child) is ConditionNode found)
                    {
                        return found;
                    }
                }
            }
            return null;
        }
    }
}
