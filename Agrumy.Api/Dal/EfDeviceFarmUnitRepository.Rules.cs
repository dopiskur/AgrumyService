using Agrumy.Dal;
using Agrumy.Shared;
using System.Text.Json;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Dal
{
    internal sealed partial class EfDeviceFarmUnitRepository
    {
        // ---- Rules (Zone/Unit/Global scope) --------------------------------------

        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForZoneAsync(int idDeviceFarmUnitZone)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmUnitZoneID == idDeviceFarmUnitZone)
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForUnitAsync(int idDeviceFarmUnit)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmUnitID == idDeviceFarmUnit)
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForFarmAsync(int idDeviceFarm)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmID == idDeviceFarm)
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForTenantGlobalAsync(int tenantId)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                // SimulationSessionID/ExperimentID excluded - those scopes have the same null Farm/Unit/Zone/Crop/Parcel shape as Global, but must never be evaluated as one.
                .Where(r => r.TenantID == tenantId && r.DeviceFarmID == null && r.DeviceFarmUnitID == null && r.DeviceFarmUnitZoneID == null
                    && r.DeviceSowingID == null && r.DeviceFarmParcelZoneID == null && r.SimulationSessionID == null && r.ExperimentID == null)
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForSowingAsync(int idSowing)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceSowingID == idSowing)
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForFarmParcelZoneAsync(int idFarmParcelZone)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmParcelZoneID == idFarmParcelZone)
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForSimulationAsync(int idSimulationSession)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.SimulationSessionID == idSimulationSession)
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForExperimentAsync(int idExperiment)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.ExperimentID == idExperiment)
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        /// One query in place of up to 6 sequential RulesGetForSimulationAsync/RulesGetForExperimentAsync/RulesGetForZoneAsync-or-RulesGetForFarmParcelZoneAsync/RulesGetForUnitAsync-or-RulesGetForSowingAsync/RulesGetForFarmAsync/RulesGetForTenantGlobalAsync calls - DeviceConfigBuilder resolves every id below FIRST (simulation/experiment membership, the zone's own farm), then calls this once and partitions the flat result back out by each row's own scope FK (already on the DTO) using the exact same filter each individual method applies. A null id means that scope contributes nothing, same as DeviceConfigBuilder just not calling that method today.
        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetForHierarchyAsync(int tenantId, int? idSimulationSession, int? idExperiment, int? idZone, int? idFarmParcelZone, int? idUnit, int? idSowing, int? idFarm, bool includeGlobal)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r =>
                    (idSimulationSession != null && r.SimulationSessionID == idSimulationSession) ||
                    (idExperiment != null && r.ExperimentID == idExperiment) ||
                    (idZone != null && r.DeviceFarmUnitZoneID == idZone) ||
                    (idFarmParcelZone != null && r.DeviceFarmParcelZoneID == idFarmParcelZone) ||
                    (idUnit != null && r.DeviceFarmUnitID == idUnit) ||
                    (idSowing != null && r.DeviceSowingID == idSowing) ||
                    (idFarm != null && r.DeviceFarmID == idFarm) ||
                    // Same SimulationSessionID/ExperimentID exclusion as RulesGetForTenantGlobalAsync - those scopes share Global's null Farm/Unit/Zone/Crop/Parcel shape but must never be evaluated as it.
                    (includeGlobal && r.TenantID == tenantId && r.DeviceFarmID == null && r.DeviceFarmUnitID == null && r.DeviceFarmUnitZoneID == null
                        && r.DeviceSowingID == null && r.DeviceFarmParcelZoneID == null && r.SimulationSessionID == null && r.ExperimentID == null))
                .OrderBy(r => r.RelayFunction).ThenBy(r => r.Name).ThenBy(r => r.IDDeviceFarmUnitZoneRule)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        /// Every Notification-action rule for the organization across all three real scopes - RuleNotificationEvaluator resolves Zone>Unit>Global itself per zone, so this deliberately returns the flat, unresolved set. Simulation/experiment-scoped ones excluded, same reasoning as RulesGetForTenantGlobalAsync above - fetched separately per zone via RulesGetForSimulationAsync/RulesGetForExperimentAsync instead.
        public async Task<IList<DeviceFarmUnitZoneRule>> RulesGetNotificationRulesForTenantAsync(int tenantId)
        {
            var rows = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.TenantID == tenantId && r.ActionType == (int)ActionType.Notification && r.SimulationSessionID == null && r.ExperimentID == null)
                .ToListAsync();
            return rows.Select(ToDtoRule).ToList();
        }

        public async Task<DeviceFarmUnitZoneRule?> RuleGetByIdAsync(int? idRule)
        {
            var row = await db.DeviceFarmUnitZoneRules.AsNoTracking().FirstOrDefaultAsync(r => r.IDDeviceFarmUnitZoneRule == idRule);
            return row == null ? null : ToDtoRule(row);
        }

        public async Task<int> RuleAddAsync(DeviceFarmUnitZoneRule rule)
        {
            var row = new DeviceFarmUnitZoneRuleRow
            {
                TenantID = rule.TenantID,
                DeviceFarmID = rule.DeviceFarmID,
                DeviceFarmUnitID = rule.DeviceFarmUnitID,
                DeviceFarmUnitZoneID = rule.DeviceFarmUnitZoneID,
                DeviceSowingID = rule.DeviceSowingID,
                DeviceFarmParcelZoneID = rule.DeviceFarmParcelZoneID,
                SimulationSessionID = rule.SimulationSessionID,
                ExperimentID = rule.ExperimentID,
                ActionType = (int)rule.ActionType,
                RelayFunction = (int?)rule.RelayFunction,
                Name = rule.Name,
                Description = rule.Description,
                RootConditionJson = JsonSerializer.Serialize(rule.Root, ConditionConfigJson.Options),
                TargetPercent = rule.TargetPercent,
                IsSafetyRule = rule.IsSafetyRule,
                NotificationSubject = rule.NotificationSubject,
                NotificationBody = rule.NotificationBody,
            };
            db.DeviceFarmUnitZoneRules.Add(row);
            await db.SaveChangesAsync();
            if (rule.DeviceFarmUnitZoneID is int idZone)
            {
                await DeviceFarmUnitZoneConfigVersionBumpAsync(idZone);
            }
            else if (rule.DeviceFarmUnitID is int idUnit)
            {
                await BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => d.DeviceFarmUnitID == idUnit));
            }
            else if (rule.DeviceFarmID is int idFarm)
            {
                var unitIdsInFarm = db.DeviceFarmUnits.AsNoTracking().Where(u => u.DeviceFarmID == idFarm).Select(u => u.IDDeviceFarmUnit);
                await BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => d.DeviceFarmUnitID != null && unitIdsInFarm.Contains(d.DeviceFarmUnitID!.Value)));
            }
            else if (rule.SimulationSessionID is int idSession)
            {
                // Only the session's own member devices, not the whole organization - a simulation rule change must not force every other device in the organization to re-fetch a config that didn't actually change for them.
                var memberIds = db.SimulationSessionDevices.AsNoTracking().Where(m => m.IDSimulationSession == idSession).Select(m => m.DeviceID);
                await BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => memberIds.Contains(d.IDDevice)));
            }
            else if (rule.ExperimentID != null)
            {
                // An experiment's rule membership is dynamic (Scope+ScopeID, not a snapshotted device list) - bumping every device in the organization is broader than strictly needed, but resolving the exact current membership here would duplicate ActiveExperimentIdForZoneAsync's own cascade for a rare admin action.
                await BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => d.TenantID == rule.TenantID));
            }
            else
            {
                await BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => d.TenantID == rule.TenantID));
            }
            return row.IDDeviceFarmUnitZoneRule;
        }

        /// Every RuleTriggered condition anywhere in the organization's rules that references ruleId - callers use this to block deleting a still-referenced rule, and RuleNotificationEvaluator uses it to find dependents of a just-fired rule.
        public async Task<IList<DeviceFarmUnitZoneRule>> RulesReferencingAsync(int ruleId, int tenantId)
        {
            var candidates = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.TenantID == tenantId && r.ActionType == (int)ActionType.Notification)
                .ToListAsync();
            var result = new List<DeviceFarmUnitZoneRule>();
            foreach (var row in candidates)
            {
                DeviceFarmUnitZoneRule dto = ToDtoRule(row);
                if (dto.Root != null && ReferencesRule(dto.Root, ruleId))
                {
                    result.Add(dto);
                }
            }
            return result;

            static bool ReferencesRule(ConditionNode node, int ruleId) =>
                (node.Type == NodeType.RuleTriggered && node.ReferencedRuleId == ruleId)
                || (node.Type == NodeType.Group && node.Children.Any(c => ReferencesRule(c, ruleId)));
        }

        public async Task RuleDeleteAsync(int idRule)
        {
            var row = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .FirstOrDefaultAsync(r => r.IDDeviceFarmUnitZoneRule == idRule);
            if (row == null) { return; }

            await db.RuleNotificationStates.Where(s => s.RuleID == idRule).ExecuteDeleteAsync();
            await db.DeviceFarmUnitZoneRules.Where(r => r.IDDeviceFarmUnitZoneRule == idRule).ExecuteDeleteAsync();

            if (row.DeviceFarmUnitZoneID is int idZone)
            {
                await DeviceFarmUnitZoneConfigVersionBumpAsync(idZone);
            }
            else if (row.DeviceFarmUnitID is int idUnit)
            {
                await BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => d.DeviceFarmUnitID == idUnit));
            }
            else if (row.SimulationSessionID is int idSession)
            {
                var memberIds = db.SimulationSessionDevices.AsNoTracking().Where(m => m.IDSimulationSession == idSession).Select(m => m.DeviceID);
                await BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => memberIds.Contains(d.IDDevice)));
            }
            else
            {
                await BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => d.TenantID == row.TenantID));
            }
        }

        /// False (not just missing) for a (rule, zone) pair with no row yet - a rule that has never fired for this zone has never been "true".
        public async Task<bool> RuleNotificationWasTrueGetAsync(int ruleId, int idDeviceFarmUnitZone) =>
            await db.RuleNotificationStates.AsNoTracking()
                .Where(s => s.RuleID == ruleId && s.DeviceFarmUnitZoneID == idDeviceFarmUnitZone)
                .Select(s => (bool?)s.WasTrue).FirstOrDefaultAsync() ?? false;

        public async Task RuleNotificationWasTrueSetAsync(int ruleId, int idDeviceFarmUnitZone, bool wasTrue, DateTime? lastFiredAtUtc)
        {
            var row = await db.RuleNotificationStates.FirstOrDefaultAsync(s => s.RuleID == ruleId && s.DeviceFarmUnitZoneID == idDeviceFarmUnitZone);
            if (row == null)
            {
                row = new RuleNotificationStateRow { RuleID = ruleId, DeviceFarmUnitZoneID = idDeviceFarmUnitZone };
                db.RuleNotificationStates.Add(row);
            }
            row.WasTrue = wasTrue;
            if (lastFiredAtUtc is DateTime firedAt)
            {
                row.LastFiredAtUtc = firedAt;
            }
            await db.SaveChangesAsync();
        }

        private static DeviceFarmUnitZoneRule ToDtoRule(DeviceFarmUnitZoneRuleRow r) => new()
        {
            IDDeviceFarmUnitZoneRule = r.IDDeviceFarmUnitZoneRule,
            TenantID = r.TenantID,
            DeviceFarmID = r.DeviceFarmID,
            DeviceFarmUnitID = r.DeviceFarmUnitID,
            DeviceFarmUnitZoneID = r.DeviceFarmUnitZoneID,
            DeviceSowingID = r.DeviceSowingID,
            DeviceFarmParcelZoneID = r.DeviceFarmParcelZoneID,
            SimulationSessionID = r.SimulationSessionID,
            ExperimentID = r.ExperimentID,
            ActionType = (ActionType)r.ActionType,
            RelayFunction = (RelayFunction?)r.RelayFunction,
            Name = r.Name,
            Description = r.Description,
            Root = JsonSerializer.Deserialize<ConditionNode>(r.RootConditionJson, ConditionConfigJson.Options),
            TargetPercent = r.TargetPercent,
            IsSafetyRule = r.IsSafetyRule,
            NotificationSubject = r.NotificationSubject,
            NotificationBody = r.NotificationBody,
        };
    }
}
