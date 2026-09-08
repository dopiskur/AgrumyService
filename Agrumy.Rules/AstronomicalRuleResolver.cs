using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;

namespace Agrumy.Rules
{
    /// Recursively replaces every Astronomical node anywhere in a rule's tree with an effective Schedule node for today's local date (roadmap #396(4) made Astronomical nestable inside a GroupNode, not just a top-level flat entry) so firmware only ever has to understand NodeType.Schedule; a rule with a node that can't be resolved today (no location set, polar day/night, or a zero/negative window) is dropped entirely rather than sent with a broken link in its tree, leaving the function's other rules intact.
    public static class AstronomicalRuleResolver
    {
        // lat/lon: the resolved location for this rule's tenant (Tenant.Latitude/Longitude, falling back to ServerConfig.WeatherLocationLat/Lon - same per-tenant-then-server-wide cascade as ScheduleTimeZone/#290), not a bare ServerConfig - a rule's own tenant may sit at a different physical site than the server-wide default (roadmap #396(6)).
        public static IList<DeviceFarmUnitZoneRule> Resolve(IList<DeviceFarmUnitZoneRule> rules, double? lat, double? lon, DateOnly localDate, int utcOffsetSeconds)
        {
            var result = new List<DeviceFarmUnitZoneRule>(rules.Count);
            foreach (DeviceFarmUnitZoneRule rule in rules)
            {
                if (rule.Root == null)
                {
                    result.Add(rule);
                    continue;
                }
                if (!ContainsAstronomical(rule.Root))
                {
                    result.Add(rule);
                    continue;
                }
                if (ResolveNode(rule.Root, lat, lon, localDate, utcOffsetSeconds) is ConditionNode resolvedRoot)
                {
                    result.Add(new DeviceFarmUnitZoneRule
                    {
                        IDDeviceFarmUnitZoneRule = rule.IDDeviceFarmUnitZoneRule,
                        TenantID = rule.TenantID,
                        DeviceFarmID = rule.DeviceFarmID,
                        DeviceFarmUnitID = rule.DeviceFarmUnitID,
                        DeviceFarmUnitZoneID = rule.DeviceFarmUnitZoneID,
                        ActionType = rule.ActionType,
                        RelayFunction = rule.RelayFunction,
                        Name = rule.Name,
                        Description = rule.Description,
                        Root = resolvedRoot,
                        IsSafetyRule = rule.IsSafetyRule,
                        NotificationSubject = rule.NotificationSubject,
                        NotificationBody = rule.NotificationBody,
                    });
                }
                // else: today's window couldn't be resolved (no location / polar day-night / collapsed) - drop the whole rule, same as before #396(4)'s nesting.
            }
            return result;
        }

        private static bool ContainsAstronomical(ConditionNode node) =>
            node.Type == NodeType.Astronomical || (node.Type == NodeType.Group && node.Children.Any(ContainsAstronomical));

        /// Null means this node (or a descendant) couldn't be resolved today - propagates up so the WHOLE rule gets dropped by the caller, never a tree with a missing branch.
        private static ConditionNode? ResolveNode(ConditionNode node, double? lat, double? lon, DateOnly localDate, int utcOffsetSeconds)
        {
            if (node.Type == NodeType.Astronomical)
            {
                return ResolveAstronomical(node, lat, lon, localDate, utcOffsetSeconds);
            }
            if (node.Type != NodeType.Group)
            {
                return node;
            }
            var resolvedChildren = new List<ConditionNode>(node.Children.Count);
            foreach (ConditionNode child in node.Children)
            {
                if (ResolveNode(child, lat, lon, localDate, utcOffsetSeconds) is not ConditionNode resolvedChild)
                {
                    return null;
                }
                resolvedChildren.Add(resolvedChild);
            }
            return new ConditionNode { Type = NodeType.Group, GroupOperator = node.GroupOperator, Children = resolvedChildren };
        }

        private static ConditionNode? ResolveAstronomical(ConditionNode node, double? lat, double? lon, DateOnly localDate, int utcOffsetSeconds)
        {
            if (lat is not double latValue || lon is not double lonValue
                || node.DaysOfWeek is not int daysOfWeek || node.SunriseOffsetMinutes is not int sunriseOffset || node.SunsetOffsetMinutes is not int sunsetOffset)
            {
                return null;
            }
            (int? sunrise, int? sunset) = SolarCalculator.Compute(localDate, latValue, lonValue, utcOffsetSeconds);
            if (sunrise is not int sunriseSeconds || sunset is not int sunsetSeconds)
            {
                return null;
            }

            int start = Math.Clamp(sunriseSeconds + sunriseOffset * 60, 0, 86399);
            int end = Math.Clamp(sunsetSeconds + sunsetOffset * 60, 0, 86400);
            int duration = end - start;
            if (duration <= 0)
            {
                return null;
            }

            return new ConditionNode { Type = NodeType.Schedule, DaysOfWeek = daysOfWeek, Start = start, Duration = duration };
        }
    }
}
