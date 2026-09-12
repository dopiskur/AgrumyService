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
    /// IDeviceFarmUnitRepository - Unit/Zone CRUD, device assignment, and the hierarchical dashboard aggregation. Needs IServerConfigRepository (dashboard's ProblemEvent settings), IDeviceRepository (fleet-cache invalidation after assign/unassign, plus its ToDto mapper), and ISowingRepository/IFarmParcelRepository (Crop/Parcel arms of DashboardAggregateGetAsync's switch - those facets deliberately do NOT depend back on this one, so this one-way dependency is safe).
    internal sealed partial class EfDeviceFarmUnitRepository(AgrumyDbContext db, IOptions<AgrumySettings> settingsOptions, IServerConfigRepository serverConfigRepository, IDeviceRepository deviceRepository, ISowingRepository sowingRepository, IFarmParcelRepository farmParcelRepository, IDeviceOutboxRepository outboxRepository) : IDeviceFarmUnitRepository
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        /// A record, not the SensorData DTO - carries only what dashboard aggregation needs.
        private sealed record UnitZoneDeviceSnapshot(
            int? DeviceFarmUnitID, int? DeviceFarmUnitZoneID, bool Enabled, bool Online, bool HasRecentProblemEvent,
            double? Temperature, double? SoilTemperature, double? Humidity, int? Moisture, int? Light,
            int? Co2, int? Tvoc, double? Barometer, double? LiquidPH, int? RainLevel, int? WaterLevel, double? Wind,
            double? Ec, double? Weight)
        {
            /// Nonlinear formula - averaged per device below, not derived from already-averaged Temperature/Humidity.
            public double? Vpd => VpdCalculator.Compute(Temperature, Humidity);
            public double? DewPoint => DewPointCalculator.Compute(Temperature, Humidity);
            public double? DewPointSpread => DewPoint is double dp ? Temperature - dp : null;
        }

        /// Event types that make a zone/unit Orange (unless it's already Red).
        private static readonly int[] ProblemEventTypeIds =
        [
            (int)DeviceEventType.AuthFailed,
            (int)DeviceEventType.ConfigSyncFailed,
            (int)DeviceEventType.CrashLoopRollback,
            (int)DeviceEventType.OtaFailed,
            (int)DeviceEventType.Crash,
        ];

        // ---- Farm CRUD -----------------------------------

        public async Task<IList<DeviceFarm>> DeviceFarmsGetAsync(int? tenantID)
        {
            IQueryable<DeviceFarmRow> q = db.DeviceFarms.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(f => f.TenantID == tenantID);
            }
            var rows = await q.OrderBy(f => f.DisplayOrder).ThenBy(f => f.IDDeviceFarm).ToListAsync();
            return rows.Select(ToDtoFarm).ToList();
        }

        public async Task<DeviceFarm?> DeviceFarmGetByIdAsync(int? idDeviceFarm)
        {
            var row = await db.DeviceFarms.AsNoTracking().FirstOrDefaultAsync(f => f.IDDeviceFarm == idDeviceFarm);
            return row == null ? null : ToDtoFarm(row);
        }

        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert, so a concurrent Add can't slip past a stale count - see Agrumy.Api.Quota.QuotaGuard.
        public Task<DeviceFarm> DeviceFarmAddAsync(DeviceFarm farm, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                int nextOrder = await db.DeviceFarms.Where(f => f.TenantID == farm.TenantID).Select(f => (int?)f.DisplayOrder).MaxAsync() ?? -1;
                var row = new DeviceFarmRow { TenantID = farm.TenantID, DeviceFarmName = farm.DeviceFarmName, FarmType = (int)farm.FarmType, DisplayOrder = nextOrder + 1 };
                db.DeviceFarms.Add(row);
                await db.SaveChangesAsync();
                return ToDtoFarm(row);
            });

        /// Idempotent, safe to call from every organization-creation path (registration, admin-created, import). A real "First farm" row lands in the DB immediately (not deferred to whenever an admin first visits Farms.cshtml) - the UI hides its name while it's still the organization's only farm, matching Farms.cshtml's own multipleFarms check. Any unit the organization already has, sitting unassigned, joins it too, so a pre-existing single-farm organization doesn't suddenly see its units listed as "unassigned" once the invisible farm underneath them appears.
        public async Task EnsureFirstFarmAsync(int tenantId)
        {
            bool hasFarm = await db.DeviceFarms.AsNoTracking().AnyAsync(f => f.TenantID == tenantId);
            if (hasFarm)
            {
                return;
            }

            var farm = new DeviceFarmRow { TenantID = tenantId, DeviceFarmName = "First farm", FarmType = (int)FarmType.Greenhouse };
            db.DeviceFarms.Add(farm);
            await db.SaveChangesAsync();

            await db.DeviceFarmUnits
                .Where(u => u.TenantID == tenantId && u.DeviceFarmID == null)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.DeviceFarmID, farm.IDDeviceFarm));
        }

        public async Task DeviceFarmUpdateAsync(DeviceFarm farm)
        {
            var row = await db.DeviceFarms.FirstOrDefaultAsync(f => f.IDDeviceFarm == farm.IDDeviceFarm);
            if (row == null)
            {
                return;
            }
            // TenantID intentionally not overwritten - same "payload cannot move to another organization" rule as DeviceFarmUnitUpdateAsync.
            row.DeviceFarmName = farm.DeviceFarmName;
            await db.SaveChangesAsync();
        }

        public async Task DeviceFarmsReorderAsync(int tenantId, IReadOnlyList<int> orderedFarmIds)
        {
            var rows = await db.DeviceFarms.Where(f => f.TenantID == tenantId && orderedFarmIds.Contains(f.IDDeviceFarm)).ToListAsync();
            var byId = rows.ToDictionary(f => f.IDDeviceFarm);
            for (int i = 0; i < orderedFarmIds.Count; i++)
            {
                if (byId.TryGetValue(orderedFarmIds[i], out DeviceFarmRow? row))
                {
                    row.DisplayOrder = i;
                }
            }
            await db.SaveChangesAsync();
        }

        /// Reverses the original "unassign, don't cascade" decision: deleting a Farm now soft-deletes it AND every Unit/Zone/Device still attached to it, all stamped with the same DeletedAtUtc so DeviceFarmRestoreAsync can undo exactly this cascade (and nothing an unrelated, independently-deleted device/unit brought with it). Rules aren't touched at all - a Unit/Zone/Farm-scope rule simply becomes unreachable while its owner is soft-deleted (nothing still-visible ever looks it up, see RuleNotificationEvaluator/DeviceConfigBuilder), and reactivates for free on restore instead of needing to be recreated.
        public async Task DeviceFarmDeleteAsync(int idDeviceFarm)
        {
            bool exists = await db.DeviceFarms.AsNoTracking().AnyAsync(f => f.IDDeviceFarm == idDeviceFarm);
            if (!exists)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            var unitIds = await db.DeviceFarmUnits.AsNoTracking().Where(u => u.DeviceFarmID == idDeviceFarm).Select(u => u.IDDeviceFarmUnit).ToListAsync();
            var zoneIds = await db.DeviceFarmUnitZones.AsNoTracking().Where(z => unitIds.Contains(z.DeviceFarmUnitID)).Select(z => z.IDDeviceFarmUnitZone).ToListAsync();

            await db.Devices
                .Where(d => (d.DeviceFarmUnitID != null && unitIds.Contains(d.DeviceFarmUnitID.Value)) || (d.DeviceFarmUnitZoneID != null && zoneIds.Contains(d.DeviceFarmUnitZoneID.Value)))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Deleted, true).SetProperty(d => d.DeletedAtUtc, now));

            await db.DeviceFarmUnitZones.Where(z => unitIds.Contains(z.DeviceFarmUnitID))
                .ExecuteUpdateAsync(s => s.SetProperty(z => z.Deleted, true).SetProperty(z => z.DeletedAtUtc, now));

            await db.DeviceFarmUnits.Where(u => u.DeviceFarmID == idDeviceFarm)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Deleted, true).SetProperty(u => u.DeletedAtUtc, now));

            await db.DeviceFarms.Where(f => f.IDDeviceFarm == idDeviceFarm)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Deleted, true).SetProperty(f => f.DeletedAtUtc, now));
        }

        /// Every soft-deleted, not-yet-Purged Farm (Purged farms move to DeviceFarmPendingPurgeGetAsync instead).
        public async Task<IList<DeviceFarm>> DeviceFarmRecycleBinGetAsync(int? tenantID)
        {
            IQueryable<DeviceFarmRow> q = db.DeviceFarms.IgnoreQueryFilters().AsNoTracking().Where(f => f.Deleted && !f.Purged);
            if (tenantID != null)
            {
                q = q.Where(f => f.TenantID == tenantID);
            }
            var rows = await q.OrderByDescending(f => f.DeletedAtUtc).ToListAsync();
            return rows.Select(ToDtoFarm).ToList();
        }

        /// Same "no organization filter, ownership check before an authorized write" role as DeviceFarmGetByIdAsync, but also sees soft-deleted rows (Purged or not) - RecycleBinApiController uses this to resolve a farm's owning organization before calling DeviceFarmRestoreAsync/DeviceFarmRecycleBinMarkPurgedAsync.
        public async Task<DeviceFarm?> DeviceFarmRecycleBinGetByIdAsync(int idDeviceFarm)
        {
            var row = await db.DeviceFarms.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(f => f.IDDeviceFarm == idDeviceFarm && f.Deleted);
            return row == null ? null : ToDtoFarm(row);
        }

        /// Marked for permanent removal, still restorable until the purge cycle actually reaps it.
        public async Task<IList<DeviceFarm>> DeviceFarmPendingPurgeGetAsync(int? tenantID)
        {
            IQueryable<DeviceFarmRow> q = db.DeviceFarms.IgnoreQueryFilters().AsNoTracking().Where(f => f.Deleted && f.Purged);
            if (tenantID != null)
            {
                q = q.Where(f => f.TenantID == tenantID);
            }
            var rows = await q.OrderByDescending(f => f.PurgedAtUtc).ToListAsync();
            return rows.Select(ToDtoFarm).ToList();
        }

        /// Only Units/Zones/Devices stamped with THIS farm's own DeletedAtUtc - anything deleted independently (e.g. a device deleted on its own before or after the farm) is left alone. Shared by Restore/MarkPurged/Purge, all three of which need the exact same cascade membership.
        private async Task<(List<int> UnitIds, List<int> ZoneIds, List<int> DeviceIds)> ResolveFarmCascadeAsync(int idDeviceFarm, DateTimeOffset deletedAt)
        {
            var unitIds = await db.DeviceFarmUnits.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.DeviceFarmID == idDeviceFarm && u.Deleted && u.DeletedAtUtc == deletedAt)
                .Select(u => u.IDDeviceFarmUnit).ToListAsync();
            var zoneIds = await db.DeviceFarmUnitZones.IgnoreQueryFilters().AsNoTracking()
                .Where(z => unitIds.Contains(z.DeviceFarmUnitID) && z.Deleted && z.DeletedAtUtc == deletedAt)
                .Select(z => z.IDDeviceFarmUnitZone).ToListAsync();
            var deviceIds = await db.Devices.IgnoreQueryFilters().AsNoTracking()
                .Where(d => d.Deleted && d.DeletedAtUtc == deletedAt
                    && ((d.DeviceFarmUnitID != null && unitIds.Contains(d.DeviceFarmUnitID.Value)) || (d.DeviceFarmUnitZoneID != null && zoneIds.Contains(d.DeviceFarmUnitZoneID.Value))))
                .Select(d => d.IDDevice).ToListAsync();
            return (unitIds, zoneIds, deviceIds);
        }

        /// Undoes DeviceFarmDeleteAsync's exact cascade (or a pending mark-for-purge) - clears BOTH Deleted and Purged. False if the farm doesn't exist, isn't soft-deleted, or belongs to a different organization.
        public async Task<bool> DeviceFarmRestoreAsync(int idDeviceFarm, int? tenantID)
        {
            var farm = await db.DeviceFarms.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(f => f.IDDeviceFarm == idDeviceFarm && f.TenantID == tenantID && f.Deleted);
            if (farm == null)
            {
                return false;
            }
            var (unitIds, zoneIds, deviceIds) = await ResolveFarmCascadeAsync(idDeviceFarm, farm.DeletedAtUtc!.Value);

            await db.Devices.IgnoreQueryFilters().Where(d => deviceIds.Contains(d.IDDevice))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Deleted, false).SetProperty(d => d.DeletedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(d => d.Purged, false).SetProperty(d => d.PurgedAtUtc, (DateTimeOffset?)null));

            await db.DeviceFarmUnitZones.IgnoreQueryFilters().Where(z => zoneIds.Contains(z.IDDeviceFarmUnitZone))
                .ExecuteUpdateAsync(s => s.SetProperty(z => z.Deleted, false).SetProperty(z => z.DeletedAtUtc, (DateTimeOffset?)null));

            await db.DeviceFarmUnits.IgnoreQueryFilters().Where(u => unitIds.Contains(u.IDDeviceFarmUnit))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Deleted, false).SetProperty(u => u.DeletedAtUtc, (DateTimeOffset?)null));

            await db.DeviceFarms.IgnoreQueryFilters().Where(f => f.IDDeviceFarm == idDeviceFarm)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Deleted, false).SetProperty(f => f.DeletedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(f => f.Purged, false).SetProperty(f => f.PurgedAtUtc, (DateTimeOffset?)null));
            return true;
        }

        /// The manual "delete permanently now" trigger; just flips the flag on the farm AND its exact soft-delete cascade of devices (so DeviceRecycleBinPurgeAsync's own Purged guard passes once the reap cycle gets to them) - the actual removal happens later.
        public async Task<bool> DeviceFarmRecycleBinMarkPurgedAsync(int idDeviceFarm, int? tenantID)
        {
            var farm = await db.DeviceFarms.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(f => f.IDDeviceFarm == idDeviceFarm && f.TenantID == tenantID && f.Deleted && !f.Purged);
            if (farm == null)
            {
                return false;
            }
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            var (_, _, deviceIds) = await ResolveFarmCascadeAsync(idDeviceFarm, farm.DeletedAtUtc!.Value);

            if (deviceIds.Count > 0)
            {
                await db.Devices.IgnoreQueryFilters().Where(d => deviceIds.Contains(d.IDDevice))
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.Purged, true).SetProperty(d => d.PurgedAtUtc, nowUtc));
            }
            await db.DeviceFarms.IgnoreQueryFilters().Where(f => f.IDDeviceFarm == idDeviceFarm)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Purged, true).SetProperty(f => f.PurgedAtUtc, nowUtc));
            return true;
        }

        /// The scheduled half of marking - same per-organization-retention logic as DeviceRecycleBinMarkPurgedByRetentionAsync (a governing TenantQuota's RecycleBinRetentionDays replaces the organization's own override entirely), cascading Purged onto the farm's devices the same way DeviceFarmRecycleBinMarkPurgedAsync does for the manual trigger.
        public async Task<int> DeviceFarmRecycleBinMarkPurgedByRetentionAsync(int serverDefaultRetentionDays, CancellationToken ct)
        {
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            var tenantRetentionDays = await db.Tenants.AsNoTracking().ToDictionaryAsync(t => t.IDTenant!.Value, t => t.RecycleBinRetentionDays, ct);
            var tenantQuotaRetentionDays = await db.TenantQuotas.AsNoTracking().ToDictionaryAsync(q => q.IDTenant, q => q.RecycleBinRetentionDays, ct);

            var candidates = await db.DeviceFarms.IgnoreQueryFilters().AsNoTracking()
                .Where(f => f.Deleted && !f.Purged)
                .Select(f => new { f.IDDeviceFarm, f.TenantID, f.DeletedAtUtc })
                .ToListAsync(ct);

            var idsToMark = candidates
                .Where(c =>
                {
                    int retentionDays = RecycleBinRetentionResolver.EffectiveRecycleBinRetentionDays(c.TenantID, tenantQuotaRetentionDays, tenantRetentionDays, serverDefaultRetentionDays);
                    return retentionDays > 0 && c.DeletedAtUtc != null && c.DeletedAtUtc <= nowUtc.AddDays(-retentionDays);
                })
                .ToList();
            if (idsToMark.Count == 0)
            {
                return 0;
            }

            foreach (var c in idsToMark)
            {
                var (_, _, deviceIds) = await ResolveFarmCascadeAsync(c.IDDeviceFarm, c.DeletedAtUtc!.Value);
                if (deviceIds.Count > 0)
                {
                    await db.Devices.IgnoreQueryFilters().Where(d => deviceIds.Contains(d.IDDevice))
                        .ExecuteUpdateAsync(s => s.SetProperty(d => d.Purged, true).SetProperty(d => d.PurgedAtUtc, nowUtc), ct);
                }
            }
            var farmIds = idsToMark.Select(c => c.IDDeviceFarm).ToList();
            return await db.DeviceFarms.IgnoreQueryFilters().Where(f => farmIds.Contains(f.IDDeviceFarm))
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Purged, true).SetProperty(f => f.PurgedAtUtc, nowUtc), ct);
        }

        public async Task<IList<(int IDDeviceFarm, int? TenantID)>> DeviceFarmPurgedIdsGetAsync()
        {
            var rows = await db.DeviceFarms.IgnoreQueryFilters().AsNoTracking().Where(f => f.Deleted && f.Purged)
                .Select(f => new { f.IDDeviceFarm, f.TenantID }).ToListAsync();
            return rows.Select(f => (f.IDDeviceFarm, f.TenantID)).ToList();
        }

        /// The purge cycle's actual, irreversible removal of the farm and its exact cascade. Devices are purged first via DeviceRecycleBinPurgeAsync (SensorData included, same as a standalone device purge), then the now-empty Zone/Unit-scope rules and the Zone/Unit/Farm rows themselves. False if the farm doesn't exist, isn't Deleted+Purged, or belongs to a different organization.
        public async Task<bool> DeviceFarmRecycleBinPurgeAsync(int idDeviceFarm, int? tenantID)
        {
            var farm = await db.DeviceFarms.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(f => f.IDDeviceFarm == idDeviceFarm && f.TenantID == tenantID && f.Deleted && f.Purged);
            if (farm == null)
            {
                return false;
            }
            var (unitIds, zoneIds, deviceIds) = await ResolveFarmCascadeAsync(idDeviceFarm, farm.DeletedAtUtc!.Value);

            foreach (int deviceId in deviceIds)
            {
                await deviceRepository.DeviceRecycleBinPurgeAsync(deviceId, tenantID);
            }

            // Farm/Unit/Zone-scope rules were left untouched by the original soft delete (a restore needed them intact) - a permanent purge has no restore to protect, so they're genuinely orphaned now and go too.
            var ruleIds = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmID == idDeviceFarm
                    || (r.DeviceFarmUnitZoneID != null && zoneIds.Contains(r.DeviceFarmUnitZoneID.Value))
                    || (r.DeviceFarmUnitID != null && unitIds.Contains(r.DeviceFarmUnitID.Value) && r.DeviceFarmUnitZoneID == null))
                .Select(r => r.IDDeviceFarmUnitZoneRule).ToListAsync();
            await db.RuleNotificationStates.Where(s => ruleIds.Contains(s.RuleID) || zoneIds.Contains(s.DeviceFarmUnitZoneID)).ExecuteDeleteAsync();
            await db.DeviceFarmUnitZoneRules.Where(r => ruleIds.Contains(r.IDDeviceFarmUnitZoneRule)).ExecuteDeleteAsync();

            await db.DeviceFarmUnitZones.IgnoreQueryFilters().Where(z => zoneIds.Contains(z.IDDeviceFarmUnitZone)).ExecuteDeleteAsync();
            await db.DeviceFarmUnits.IgnoreQueryFilters().Where(u => unitIds.Contains(u.IDDeviceFarmUnit)).ExecuteDeleteAsync();
            await db.DeviceFarms.IgnoreQueryFilters().Where(f => f.IDDeviceFarm == idDeviceFarm).ExecuteDeleteAsync();
            return true;
        }

        // ---- Unit CRUD -------------------------------------------------

        public async Task<IList<DeviceFarmUnit>> DeviceFarmUnitsGetAsync(int? tenantID)
        {
            IQueryable<DeviceFarmUnitRow> q = db.DeviceFarmUnits.AsNoTracking();
            if (tenantID != null)
            {
                q = q.Where(u => u.TenantID == tenantID);
            }
            var rows = await q.OrderBy(u => u.DisplayOrder).ThenBy(u => u.DeviceFarmUnitName).ToListAsync();
            return rows.Select(ToDtoUnit).ToList();
        }

        public async Task<DeviceFarmUnit?> DeviceFarmUnitGetByIdAsync(int? idDeviceFarmUnit)
        {
            var row = await db.DeviceFarmUnits.AsNoTracking().FirstOrDefaultAsync(u => u.IDDeviceFarmUnit == idDeviceFarmUnit);
            return row == null ? null : ToDtoUnit(row);
        }

        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert, so a concurrent Add can't slip past a stale count - see Agrumy.Api.Quota.QuotaGuard.
        public Task<DeviceFarmUnit> DeviceFarmUnitAddAsync(DeviceFarmUnit unit, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, () => InsertUnitAsync(unit));

        private async Task<DeviceFarmUnit> InsertUnitAsync(DeviceFarmUnit unit)
        {
            int nextOrder = await db.DeviceFarmUnits.Where(u => u.TenantID == unit.TenantID).Select(u => (int?)u.DisplayOrder).MaxAsync() ?? -1;
            var row = new DeviceFarmUnitRow { TenantID = unit.TenantID, DeviceFarmUnitName = unit.DeviceFarmUnitName, DeviceFarmID = unit.DeviceFarmID, DisplayOrder = nextOrder + 1 };
            db.DeviceFarmUnits.Add(row);
            await db.SaveChangesAsync();
            return ToDtoUnit(row);
        }

        public async Task DeviceFarmUnitUpdateAsync(DeviceFarmUnit unit)
        {
            var row = await db.DeviceFarmUnits.FirstOrDefaultAsync(u => u.IDDeviceFarmUnit == unit.IDDeviceFarmUnit);
            if (row == null)
            {
                return;
            }
            // TenantID intentionally not overwritten - same "payload cannot move to another organization" rule as DeviceUpdateAsync.
            row.DeviceFarmUnitName = unit.DeviceFarmUnitName;
            row.DeviceFarmID = unit.DeviceFarmID;
            await db.SaveChangesAsync();
        }

        /// Scoped to whatever subset the caller drags (one farm's units, or the unassigned bucket) - reused 0..N-1 indices across different farms never collide because DeviceFarmUnitDashboardGetAsync's consumers always filter by DeviceFarmID before comparing DisplayOrder.
        public async Task DeviceFarmUnitsReorderAsync(int tenantId, IReadOnlyList<int> orderedUnitIds)
        {
            var rows = await db.DeviceFarmUnits.Where(u => u.TenantID == tenantId && orderedUnitIds.Contains(u.IDDeviceFarmUnit)).ToListAsync();
            var byId = rows.ToDictionary(u => u.IDDeviceFarmUnit);
            for (int i = 0; i < orderedUnitIds.Count; i++)
            {
                if (byId.TryGetValue(orderedUnitIds[i], out DeviceFarmUnitRow? row))
                {
                    row.DisplayOrder = i;
                }
            }
            await db.SaveChangesAsync();
        }

        public async Task DeviceFarmUnitDeleteAsync(int idDeviceFarmUnit)
        {
            var zoneIds = await db.DeviceFarmUnitZones.AsNoTracking()
                .Where(z => z.DeviceFarmUnitID == idDeviceFarmUnit)
                .Select(z => z.IDDeviceFarmUnitZone)
                .ToListAsync();

            foreach (int zoneId in zoneIds)
            {
                await DeviceFarmUnitZoneDeleteAsync(zoneId);
            }

            // Unit-scope rules (DeviceFarmUnitZoneID == null) live directly on the unit, not any of its zones - the zone loop above never touches them, so they'd otherwise survive as orphans after the unit is gone.
            var unitRuleIds = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmUnitID == idDeviceFarmUnit && r.DeviceFarmUnitZoneID == null).Select(r => r.IDDeviceFarmUnitZoneRule).ToListAsync();
            await db.RuleNotificationStates.Where(s => unitRuleIds.Contains(s.RuleID)).ExecuteDeleteAsync();
            await db.DeviceFarmUnitZoneRules.Where(r => r.DeviceFarmUnitID == idDeviceFarmUnit && r.DeviceFarmUnitZoneID == null).ExecuteDeleteAsync();

            await db.DeviceFarmUnits.Where(u => u.IDDeviceFarmUnit == idDeviceFarmUnit).ExecuteDeleteAsync();
        }

        private static DeviceFarmUnit ToDtoUnit(DeviceFarmUnitRow u) => new()
        {
            IDDeviceFarmUnit = u.IDDeviceFarmUnit,
            TenantID = u.TenantID,
            DeviceFarmUnitName = u.DeviceFarmUnitName,
            DeviceFarmID = u.DeviceFarmID,
            DisplayOrder = u.DisplayOrder,
        };

        private static DeviceFarm ToDtoFarm(DeviceFarmRow f) => new()
        {
            IDDeviceFarm = f.IDDeviceFarm,
            TenantID = f.TenantID,
            DeviceFarmName = f.DeviceFarmName,
            FarmType = (FarmType)f.FarmType,
            DisplayOrder = f.DisplayOrder,
            FarmGroupID = f.FarmGroupID,
            DeletedAtUtc = f.DeletedAtUtc,
            PurgedAtUtc = f.PurgedAtUtc,
        };
    }
}
