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
            DateTime utcNow, int utcOffsetSeconds, Func<int, bool> referencedRuleFiredThisTick) =>
            rule.Root != null && EvaluateNode(rule.Root, wasRuleTrue, readMetric, utcNow, utcOffsetSeconds, referencedRuleFiredThisTick);

        public static bool EvaluateNode(ConditionNode node, bool wasRuleTrue, Func<SensorMetric, double?> readMetric,
            DateTime utcNow, int utcOffsetSeconds, Func<int, bool> referencedRuleFiredThisTick)
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
                case NodeType.Group:
                {
                    if (node.Children.Count == 0)
                    {
                        return false;
                    }
                    bool result = EvaluateNode(node.Children[0], wasRuleTrue, readMetric, utcNow, utcOffsetSeconds, referencedRuleFiredThisTick);
                    for (int i = 1; i < node.Children.Count; i++)
                    {
                        bool next = EvaluateNode(node.Children[i], wasRuleTrue, readMetric, utcNow, utcOffsetSeconds, referencedRuleFiredThisTick);
                        result = node.GroupOperator == LogicalOperator.And ? (result && next) : (result || next);
                    }
                    return result;
                }
                case NodeType.Astronomical:
                default:
                    // Astronomical never reaches evaluation as-is - AstronomicalRuleResolver compiles every occurrence into a Schedule node before a rule is evaluated (Relay path) or would need the same treatment on the Notification path (not currently resolved there - see DeviceFarmUnitApiController's validation, which rejects Astronomical on a Notification rule).
                    return false;
            }
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
