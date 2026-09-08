using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Devices
{
    /// Best-effort, non-blocking heads-up when a newly saved rule's RelayFunction/Name already has a rule sitting at a DIFFERENT scope somewhere in its ancestor/descendant chain. RuleHierarchyResolver already picks a silent Zone&gt;Unit&gt;Farm&gt;Global winner regardless of this check - it only surfaces that a shadow situation now exists so the admin isn't surprised later. Walking every unit/zone under a Farm/Global rule is N+1 by design (one save, not a hot path) - fine at greenhouse-install scale, not meant for a tenant with thousands of zones.
    public class RuleScopeConflictService(IDeviceFarmUnitRepository repo)
    {
        public async Task<string?> FindConflictWarningAsync(DeviceFarmUnitZoneRule rule)
        {
            var conflicts = new List<string>();

            if (rule.DeviceFarmUnitZoneID is int zoneId)
            {
                DeviceFarmUnitZone? zone = await repo.DeviceFarmUnitZoneGetByIdAsync(zoneId);
                DeviceFarmUnit? unit = zone?.DeviceFarmUnitID is int uid ? await repo.DeviceFarmUnitGetByIdAsync(uid) : null;
                if (unit?.IDDeviceFarmUnit is int u && Matches(rule, await repo.RulesGetForUnitAsync(u)))
                {
                    conflicts.Add("its unit");
                }
                if (unit?.DeviceFarmID is int f && Matches(rule, await repo.RulesGetForFarmAsync(f)))
                {
                    conflicts.Add("its farm");
                }
                if (Matches(rule, await repo.RulesGetForTenantGlobalAsync(rule.TenantID)))
                {
                    conflicts.Add("Global");
                }
            }
            else if (rule.DeviceFarmUnitID is int unitId)
            {
                DeviceFarmUnit? unit = await repo.DeviceFarmUnitGetByIdAsync(unitId);
                if (unit?.DeviceFarmID is int f && Matches(rule, await repo.RulesGetForFarmAsync(f)))
                {
                    conflicts.Add("its farm");
                }
                if (Matches(rule, await repo.RulesGetForTenantGlobalAsync(rule.TenantID)))
                {
                    conflicts.Add("Global");
                }
                await AddZoneConflictsAsync(rule, unitId, conflicts);
            }
            else if (rule.DeviceFarmID is int farmId)
            {
                if (Matches(rule, await repo.RulesGetForTenantGlobalAsync(rule.TenantID)))
                {
                    conflicts.Add("Global");
                }
                var units = (await repo.DeviceFarmUnitsGetAsync(rule.TenantID)).Where(u => u.DeviceFarmID == farmId).ToList();
                foreach (DeviceFarmUnit unit in units)
                {
                    if (unit.IDDeviceFarmUnit is not int uid)
                    {
                        continue;
                    }
                    if (Matches(rule, await repo.RulesGetForUnitAsync(uid)))
                    {
                        conflicts.Add($"unit \"{unit.DeviceFarmUnitName}\"");
                    }
                    await AddZoneConflictsAsync(rule, uid, conflicts);
                }
            }
            else // Global
            {
                foreach (DeviceFarm farm in await repo.DeviceFarmsGetAsync(rule.TenantID))
                {
                    if (farm.IDDeviceFarm is int fid && Matches(rule, await repo.RulesGetForFarmAsync(fid)))
                    {
                        conflicts.Add($"farm \"{farm.DeviceFarmName}\"");
                    }
                }
                foreach (DeviceFarmUnit unit in await repo.DeviceFarmUnitsGetAsync(rule.TenantID))
                {
                    if (unit.IDDeviceFarmUnit is not int uid)
                    {
                        continue;
                    }
                    if (Matches(rule, await repo.RulesGetForUnitAsync(uid)))
                    {
                        conflicts.Add($"unit \"{unit.DeviceFarmUnitName}\"");
                    }
                    await AddZoneConflictsAsync(rule, uid, conflicts);
                }
            }

            if (conflicts.Count == 0)
            {
                return null;
            }
            string target = rule.ActionType == ActionType.Relay ? $"{rule.RelayFunction}" : $"\"{rule.Name}\"";
            return $"{target} already has a rule at {string.Join(", ", conflicts.Distinct())} - the more specific scope will silently win for the zones they share.";
        }

        private async Task AddZoneConflictsAsync(DeviceFarmUnitZoneRule rule, int idDeviceFarmUnit, List<string> conflicts)
        {
            foreach (DeviceFarmUnitZone zone in await repo.DeviceFarmUnitZonesGetAsync(idDeviceFarmUnit))
            {
                if (zone.IDDeviceFarmUnitZone is int zid && Matches(rule, await repo.RulesGetForZoneAsync(zid)))
                {
                    conflicts.Add($"zone \"{zone.DeviceFarmUnitZoneName}\"");
                }
            }
        }

        private static bool Matches(DeviceFarmUnitZoneRule rule, IList<DeviceFarmUnitZoneRule> candidates) =>
            rule.ActionType == ActionType.Relay
                ? candidates.Any(r => r.ActionType == ActionType.Relay && r.RelayFunction == rule.RelayFunction)
                : candidates.Any(r => r.ActionType == ActionType.Notification && r.Name == rule.Name);
    }
}
