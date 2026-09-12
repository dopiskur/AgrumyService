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
        // ---- Zone CRUD ------------------------------------------------

        public async Task<IList<DeviceFarmUnitZone>> DeviceFarmUnitZonesGetAsync(int idDeviceFarmUnit)
        {
            var rows = await db.DeviceFarmUnitZones.AsNoTracking()
                .Where(z => z.DeviceFarmUnitID == idDeviceFarmUnit)
                .OrderBy(z => z.DeviceFarmUnitZoneName)
                .ToListAsync();
            return rows.Select(ToDtoZone).ToList();
        }

        public async Task<DeviceFarmUnitZone?> DeviceFarmUnitZoneGetByIdAsync(int? idDeviceFarmUnitZone)
        {
            var row = await db.DeviceFarmUnitZones.AsNoTracking().FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
            return row == null ? null : ToDtoZone(row);
        }

        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert, so a concurrent Add can't slip past a stale count - see Agrumy.Api.Quota.QuotaGuard.
        public Task<DeviceFarmUnitZone> DeviceFarmUnitZoneAddAsync(DeviceFarmUnitZone zone, Func<Task<string?>>? quotaCheckAsync = null) =>
            QuotaGuard.RunAsync(db, quotaCheckAsync, () => InsertZoneAsync(zone));

        private async Task<DeviceFarmUnitZone> InsertZoneAsync(DeviceFarmUnitZone zone)
        {
            var row = new DeviceFarmUnitZoneRow
            {
                TenantID = zone.TenantID,
                DeviceFarmUnitID = zone.DeviceFarmUnitID,
                DeviceFarmUnitZoneName = zone.DeviceFarmUnitZoneName,
                WaterPumpMaxRunSeconds = settings.WaterPumpMaxRunSeconds,
                WaterPumpCooldownSeconds = settings.WaterPumpCooldownSeconds,
                // No server-wide default makes sense for a specific tank's own calibration - unlike WaterPumpMaxRunSeconds above, always taken from the caller (null/unset is the correct "no tank tracking yet" state).
                TankCapacityLiters = zone.TankCapacityLiters,
                WaterLevelRawEmpty = zone.WaterLevelRawEmpty,
                WaterLevelRawFull = zone.WaterLevelRawFull,
                WaterPumpMinLevel = zone.WaterPumpMinLevel,
                // Same reasoning as Tank* above - no server-wide default, always taken from the caller.
                HeatingMaxRunSeconds = zone.HeatingMaxRunSeconds,
                VentilationMaxRunSeconds = zone.VentilationMaxRunSeconds,
                HeatingFailSafePolicy = (int?)zone.HeatingFailSafePolicy,
            };
            db.DeviceFarmUnitZones.Add(row);
            await db.SaveChangesAsync();
            return ToDtoZone(row);
        }

        public async Task DeviceFarmUnitZoneUpdateAsync(DeviceFarmUnitZone zone)
        {
            var row = await db.DeviceFarmUnitZones.FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == zone.IDDeviceFarmUnitZone);
            if (row == null)
            {
                return;
            }
            // TenantID/DeviceFarmUnitID intentionally not overwritten - renaming a zone must not silently move it to another unit or tenant.
            row.DeviceFarmUnitZoneName = zone.DeviceFarmUnitZoneName;
            row.WaterPumpMaxRunSeconds = zone.WaterPumpMaxRunSeconds;
            row.WaterPumpCooldownSeconds = zone.WaterPumpCooldownSeconds;
            row.SkipWaterPumpWhenRainPredicted = zone.SkipWaterPumpWhenRainPredicted;
            row.TankCapacityLiters = zone.TankCapacityLiters;
            row.WaterLevelRawEmpty = zone.WaterLevelRawEmpty;
            row.WaterLevelRawFull = zone.WaterLevelRawFull;
            row.WaterPumpMinLevel = zone.WaterPumpMinLevel;
            row.HeatingMaxRunSeconds = zone.HeatingMaxRunSeconds;
            row.VentilationMaxRunSeconds = zone.VentilationMaxRunSeconds;
            row.HeatingFailSafePolicy = (int?)zone.HeatingFailSafePolicy;
            await db.SaveChangesAsync();
            await DeviceFarmUnitZoneConfigVersionBumpAsync(idDeviceFarmUnitZone: row.IDDeviceFarmUnitZone);
        }

        public async Task<bool> DeviceFarmUnitZoneMigrateAsync(int idDeviceFarmUnitZone, int idTargetDeviceFarmUnit)
        {
            var row = await db.DeviceFarmUnitZones.FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
            if (row == null)
            {
                return false;
            }
            int? tenantID = row.TenantID;
            row.DeviceFarmUnitID = idTargetDeviceFarmUnit;
            await db.SaveChangesAsync();
            // DeviceFarmUnitID is denormalized onto DeviceRow (DeviceAssignToZoneAsync's own copy) - the zone's own FK above isn't what unit-scope rule resolution reads, this is.
            await db.Devices.Where(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeviceFarmUnitID, idTargetDeviceFarmUnit));
            await DeviceFarmUnitZoneConfigVersionBumpAsync(idDeviceFarmUnitZone);
            await deviceRepository.InvalidateFleetCacheAsync(tenantID);
            return true;
        }

        /// Bumps ConfigVersion for every device in the zone (bulk update, not fetch-then-loop) so the next poll picks up a zone-level rule/safety-limit change, and enqueues each of those devices' own ConfigChanged outbox signal.
        public Task DeviceFarmUnitZoneConfigVersionBumpAsync(int idDeviceFarmUnitZone) =>
            BumpConfigVersionAndMarkChangedAsync(db.Devices.Where(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone));

        /// Shared by every rule/safety-limit change that must reach a whole set of devices: bumps their (informational) ConfigVersion in one bulk statement, then enqueues one ConfigChanged outbox item per device (the actual resend trigger) - device IDs are fetched once up front since ExecuteUpdateAsync alone never returns which rows it touched.
        private async Task BumpConfigVersionAndMarkChangedAsync(IQueryable<DeviceRow> devices)
        {
            List<int> deviceIds = await devices.Select(d => d.IDDevice).ToListAsync();
            if (deviceIds.Count == 0)
            {
                return;
            }
            await db.Devices.Where(d => deviceIds.Contains(d.IDDevice))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.ConfigVersion, d => (d.ConfigVersion ?? 0) + 1));
            foreach (int deviceId in deviceIds)
            {
                await outboxRepository.AddOutboxItemAsync(deviceId, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
            }
        }

        public async Task DeviceFarmUnitZoneDeleteAsync(int idDeviceFarmUnitZone)
        {
            var deviceIds = await db.Devices.AsNoTracking()
                .Where(d => d.DeviceFarmUnitZoneID == idDeviceFarmUnitZone)
                .Select(d => d.IDDevice)
                .ToListAsync();

            foreach (int deviceId in deviceIds)
            {
                await DeviceUnassignFromZoneAsync(deviceId);
            }

            // App-level cleanup, not a DB-level CASCADE - see AgrumyDbContext's DeviceFarmUnitZoneRuleRow config, DeleteBehavior.NoAction. Zone-cascade deletion does not run the RulesReferencingAsync guard RuleDeleteAsync uses - a whole-zone delete already unassigns its devices unconditionally, same "cascade wins" precedent.
            var ruleIds = await db.DeviceFarmUnitZoneRules.AsNoTracking()
                .Where(r => r.DeviceFarmUnitZoneID == idDeviceFarmUnitZone).Select(r => r.IDDeviceFarmUnitZoneRule).ToListAsync();
            await db.RuleNotificationStates.Where(s => ruleIds.Contains(s.RuleID) || s.DeviceFarmUnitZoneID == idDeviceFarmUnitZone).ExecuteDeleteAsync();
            await db.DeviceFarmUnitZoneRules.Where(r => r.DeviceFarmUnitZoneID == idDeviceFarmUnitZone).ExecuteDeleteAsync();

            await db.DeviceFarmUnitZones.Where(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone).ExecuteDeleteAsync();
        }

        private static DeviceFarmUnitZone ToDtoZone(DeviceFarmUnitZoneRow z) => new()
        {
            IDDeviceFarmUnitZone = z.IDDeviceFarmUnitZone,
            TenantID = z.TenantID,
            DeviceFarmUnitID = z.DeviceFarmUnitID,
            DeviceFarmUnitZoneName = z.DeviceFarmUnitZoneName,
            WaterPumpMaxRunSeconds = z.WaterPumpMaxRunSeconds,
            WaterPumpCooldownSeconds = z.WaterPumpCooldownSeconds,
            SkipWaterPumpWhenRainPredicted = z.SkipWaterPumpWhenRainPredicted,
            TankCapacityLiters = z.TankCapacityLiters,
            WaterLevelRawEmpty = z.WaterLevelRawEmpty,
            WaterLevelRawFull = z.WaterLevelRawFull,
            WaterPumpMinLevel = z.WaterPumpMinLevel,
            HeatingMaxRunSeconds = z.HeatingMaxRunSeconds,
            VentilationMaxRunSeconds = z.VentilationMaxRunSeconds,
            HeatingFailSafePolicy = (HeatingFailSafePolicyType?)z.HeatingFailSafePolicy,
            DashboardWidgets = string.IsNullOrEmpty(z.DashboardWidgetsJson)
                ? []
                : JsonSerializer.Deserialize<List<DashboardWidget>>(z.DashboardWidgetsJson, ConditionConfigJson.Options) ?? [],
            DashboardGridColumns = z.DashboardGridColumns,
        };

        /// Saves independently of DeviceFarmUnitZoneUpdateAsync (no ConfigVersion bump - a display-only layout never reaches the device).
        public async Task DeviceFarmUnitZoneWidgetsSetAsync(int idDeviceFarmUnitZone, List<DashboardWidget> widgets)
        {
            var row = await db.DeviceFarmUnitZones.FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
            if (row == null)
            {
                return;
            }
            row.DashboardWidgetsJson = JsonSerializer.Serialize(widgets, ConditionConfigJson.Options);
            await db.SaveChangesAsync();
        }

        /// Same independent-save reasoning as DeviceFarmUnitZoneWidgetsSetAsync.
        public async Task DeviceFarmUnitZoneGridColumnsSetAsync(int idDeviceFarmUnitZone, int columns)
        {
            var row = await db.DeviceFarmUnitZones.FirstOrDefaultAsync(z => z.IDDeviceFarmUnitZone == idDeviceFarmUnitZone);
            if (row == null)
            {
                return;
            }
            row.DashboardGridColumns = columns;
            await db.SaveChangesAsync();
        }
    }
}
