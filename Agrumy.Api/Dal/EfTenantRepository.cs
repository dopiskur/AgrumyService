using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    /// ITenantRepository - a leaf facet, no dependency on any other domain.
    internal sealed class EfTenantRepository(AgrumyDbContext db, ISecretProtector secretProtector) : ITenantRepository
    {
        public async Task<bool> TenantGetAsync(string tenantName)
        {
            return await db.Tenants.AsNoTracking().AnyAsync(t => t.TenantName == tenantName);
        }

        public async Task<int?> TenantGetIdAsync(string tenantName)
        {
            return await db.Tenants.AsNoTracking()
                .Where(t => t.TenantName == tenantName)
                .Select(t => (int?)t.IDTenant)
                .FirstOrDefaultAsync();
        }

        public async Task<int> TenantAddAsync(string tenantName)
        {
            var row = new TenantRow { TenantName = tenantName };
            db.Tenants.Add(row);
            await db.SaveChangesAsync();
            return row.IDTenant;
        }

        public async Task<IList<Tenant>> TenantsGetAllAsync()
        {
            return await db.Tenants.AsNoTracking()
                .OrderBy(t => t.TenantName)
                .Select(t => new Tenant { IDTenant = t.IDTenant, TenantName = t.TenantName, ScheduleTimeZone = t.ScheduleTimeZone, Latitude = t.Latitude, Longitude = t.Longitude, EmergencyStopActive = t.EmergencyStopActive, RecycleBinRetentionDays = t.RecycleBinRetentionDays })
                .ToListAsync();
        }

        public async Task<Tenant?> TenantGetByIdAsync(int idTenant)
        {
            var row = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.IDTenant == idTenant);
            return row == null ? null : new Tenant { IDTenant = row.IDTenant, TenantName = row.TenantName, ScheduleTimeZone = row.ScheduleTimeZone, Latitude = row.Latitude, Longitude = row.Longitude, EmergencyStopActive = row.EmergencyStopActive, RecycleBinRetentionDays = row.RecycleBinRetentionDays };
        }

        public async Task TenantUpdateAsync(Tenant tenant)
        {
            var row = await db.Tenants.FirstOrDefaultAsync(t => t.IDTenant == tenant.IDTenant);
            if (row == null)
            {
                return;
            }
            row.TenantName = tenant.TenantName ?? row.TenantName;
            row.ScheduleTimeZone = tenant.ScheduleTimeZone;
            row.Latitude = tenant.Latitude;
            row.Longitude = tenant.Longitude;
            row.RecycleBinRetentionDays = tenant.RecycleBinRetentionDays;
            // EmergencyStopActive deliberately NOT written here - TenantEmergencyStopSetAsync is its only writer, so a stale rename/timezone form post can't silently clear or set it.
            await db.SaveChangesAsync();
        }

        /// The only writer of EmergencyStopActive - also bumps ConfigVersion for every device in the tenant so the change reaches them on their VERY NEXT poll instead of waiting for ConfigHeartbeatHours, since a fail-closed safety switch can't tolerate that latency.
        public async Task TenantEmergencyStopSetAsync(int idTenant, bool active)
        {
            var row = await db.Tenants.FirstOrDefaultAsync(t => t.IDTenant == idTenant);
            if (row == null)
            {
                return;
            }
            row.EmergencyStopActive = active;
            await db.SaveChangesAsync();

            await db.Devices.Where(d => d.TenantID == idTenant)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.ConfigVersion, d => (d.ConfigVersion ?? 0) + 1));
        }

        public async Task<bool> TenantDeleteAsync(int idTenant, bool deleteUsers)
        {
            if (deleteUsers)
            {
                // Same FK cleanup UserDeleteAsync needs (userRefreshToken's FK is NoAction, so it must be cleared explicitly); userUserRole's FK is Cascade.
                var idsToDelete = await db.Users.AsNoTracking().Where(u => u.TenantID == idTenant).Select(u => u.IDUser).ToListAsync();
                if (idsToDelete.Count > 0)
                {
                    await db.RefreshTokens.Where(t => idsToDelete.Contains(t.UserID)).ExecuteDeleteAsync();
                    await db.Users.Where(u => u.TenantID == idTenant).ExecuteDeleteAsync();
                }
            }
            else
            {
                await db.Users.Where(u => u.TenantID == idTenant).ExecuteUpdateAsync(s => s.SetProperty(u => u.TenantID, (int?)null));
            }

            await db.TenantWifiConfigs.Where(c => c.TenantID == idTenant).ExecuteDeleteAsync();

            int rows = await db.Tenants.Where(t => t.IDTenant == idTenant).ExecuteDeleteAsync();
            return rows > 0;
        }

        public async Task<TenantQuota?> TenantQuotaGetAsync(int idTenant)
        {
            if (idTenant == 0)
            {
                return null;
            }
            var row = await db.TenantQuotas.AsNoTracking().FirstOrDefaultAsync(q => q.IDTenant == idTenant);
            return row == null ? TenantQuota.Default(idTenant) : ToDto(row);
        }

        public async Task TenantQuotaSetAsync(TenantQuota quota)
        {
            var row = await db.TenantQuotas.FirstOrDefaultAsync(q => q.IDTenant == quota.IDTenant);
            if (row == null)
            {
                row = new TenantQuotaRow { IDTenant = quota.IDTenant };
                db.TenantQuotas.Add(row);
            }
            row.MaxDevices = quota.MaxDevices;
            row.MaxFarms = quota.MaxFarms;
            row.MaxUnits = quota.MaxUnits;
            row.MaxZones = quota.MaxZones;
            row.MaxControllersPerDevice = quota.MaxControllersPerDevice;
            row.MaxSensorsPerDevice = quota.MaxSensorsPerDevice;
            row.MqttEnabled = quota.MqttEnabled;
            row.LoRaEnabled = quota.LoRaEnabled;
            row.GatewayEnabled = quota.GatewayEnabled;
            row.MinSensorIntervalMinutes = quota.MinSensorIntervalMinutes;
            row.MaxDataRetentionDays = quota.MaxDataRetentionDays;
            row.RecycleBinRetentionDays = quota.RecycleBinRetentionDays;
            row.MaxUsers = quota.MaxUsers;
            row.MaxSimulations = quota.MaxSimulations;
            await db.SaveChangesAsync();
        }

        private static TenantQuota ToDto(TenantQuotaRow row) => new()
        {
            IDTenant = row.IDTenant,
            MaxDevices = row.MaxDevices,
            MaxFarms = row.MaxFarms,
            MaxUnits = row.MaxUnits,
            MaxZones = row.MaxZones,
            MaxControllersPerDevice = row.MaxControllersPerDevice,
            MaxSensorsPerDevice = row.MaxSensorsPerDevice,
            MqttEnabled = row.MqttEnabled,
            LoRaEnabled = row.LoRaEnabled,
            GatewayEnabled = row.GatewayEnabled,
            MinSensorIntervalMinutes = row.MinSensorIntervalMinutes,
            MaxDataRetentionDays = row.MaxDataRetentionDays,
            RecycleBinRetentionDays = row.RecycleBinRetentionDays,
            MaxUsers = row.MaxUsers,
            MaxSimulations = row.MaxSimulations,
        };

        public async Task<bool> TenantZeroIsEmptyAsync()
        {
            if (await db.Devices.AsNoTracking().AnyAsync(d => d.TenantID == 0))
            {
                return false;
            }
            var tenant0Users = await db.Users.AsNoTracking().Where(u => u.TenantID == 0)
                .Select(u => u.PwdHash).ToListAsync();
            return tenant0Users.Count == 0 || (tenant0Users.Count == 1 && tenant0Users[0] == null);
        }

        public async Task<IList<TenantWifiConfig>> TenantWifiConfigsGetAsync(int tenantID)
        {
            var rows = await db.TenantWifiConfigs.AsNoTracking()
                .Where(c => c.TenantID == tenantID)
                .ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<TenantWifiConfig> TenantWifiConfigAddAsync(TenantWifiConfig config)
        {
            var row = new TenantWifiConfigRow { TenantID = config.TenantID, Ssid = config.Ssid, Password = secretProtector.Protect(config.Password) ?? "" };
            db.TenantWifiConfigs.Add(row);
            await db.SaveChangesAsync();
            return ToDto(row);
        }

        public async Task<TenantWifiConfig?> TenantWifiConfigGetByIdAsync(int idTenantWifiConfig)
        {
            var row = await db.TenantWifiConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.IDTenantWifiConfig == idTenantWifiConfig);
            return row == null ? null : ToDto(row);
        }

        /// Blank Password means "leave the stored password alone" - the caller-facing DTO never carries the real value back (see DiscoveryApiController.WifiConfigs), so blank can only mean "unchanged", never "clear it".
        public async Task TenantWifiConfigUpdateAsync(TenantWifiConfig config)
        {
            var row = await db.TenantWifiConfigs.FirstOrDefaultAsync(c => c.IDTenantWifiConfig == config.IDTenantWifiConfig);
            if (row == null)
            {
                return;
            }
            row.Ssid = config.Ssid;
            if (!string.IsNullOrEmpty(config.Password))
            {
                row.Password = secretProtector.Protect(config.Password) ?? "";
            }
            await db.SaveChangesAsync();
        }

        private TenantWifiConfig ToDto(TenantWifiConfigRow row) => new()
        {
            IDTenantWifiConfig = row.IDTenantWifiConfig,
            TenantID = row.TenantID,
            Ssid = row.Ssid,
            Password = secretProtector.Unprotect(row.Password),
        };

        public async Task TenantWifiConfigDeleteAsync(int idTenantWifiConfig)
        {
            await db.TenantWifiConfigs.Where(c => c.IDTenantWifiConfig == idTenantWifiConfig).ExecuteDeleteAsync();
        }

        public async Task TenantUsageSnapshotRecordAsync(int idTenant, DateTimeOffset snapshotDateUtc)
        {
            int deviceCount = await db.Devices.CountAsync(d => d.TenantID == idTenant);
            long sensorDataRowCount = await db.SensorData.LongCountAsync(s => s.TenantID == idTenant);

            var row = await db.TenantUsageSnapshots
                .FirstOrDefaultAsync(x => x.TenantID == idTenant && x.SnapshotDateUtc == snapshotDateUtc);
            if (row == null)
            {
                row = new TenantUsageSnapshotRow { TenantID = idTenant, SnapshotDateUtc = snapshotDateUtc };
                db.TenantUsageSnapshots.Add(row);
            }
            row.DeviceCount = deviceCount;
            row.SensorDataRowCount = sensorDataRowCount;
            await db.SaveChangesAsync();
        }

        public async Task<TenantAlertConfig> TenantAlertConfigGetAsync(int idTenant)
        {
            var row = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.IDTenant == idTenant);
            return row == null ? new TenantAlertConfig() : new TenantAlertConfig
            {
                ProblemEventAlertsEnabled = row.ProblemEventAlertsEnabled,
                ProblemEventExpiryHours = row.ProblemEventExpiryHours,
                BatteryLowThreshold = row.BatteryLowThreshold,
                BatteryLowHysteresis = row.BatteryLowHysteresis,
                TankRefillThreshold = row.TankRefillThreshold,
                TankRefillHysteresis = row.TankRefillHysteresis,
                EventDedupeMinutes = row.EventDedupeMinutes,
            };
        }

        public async Task TenantAlertConfigUpdateAsync(int idTenant, TenantAlertConfig config)
        {
            var row = await db.Tenants.FirstOrDefaultAsync(t => t.IDTenant == idTenant);
            if (row == null)
            {
                return;
            }
            row.ProblemEventAlertsEnabled = config.ProblemEventAlertsEnabled;
            row.ProblemEventExpiryHours = config.ProblemEventExpiryHours;
            row.BatteryLowThreshold = config.BatteryLowThreshold;
            row.BatteryLowHysteresis = config.BatteryLowHysteresis;
            row.TankRefillThreshold = config.TankRefillThreshold;
            row.TankRefillHysteresis = config.TankRefillHysteresis;
            row.EventDedupeMinutes = config.EventDedupeMinutes;
            await db.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<TenantUsageSnapshot>> TenantUsageSnapshotsGetAsync(int idTenant, int days)
        {
            return await db.TenantUsageSnapshots.AsNoTracking()
                .Where(x => x.TenantID == idTenant)
                .OrderByDescending(x => x.SnapshotDateUtc)
                .Take(days)
                .Select(x => new TenantUsageSnapshot
                {
                    IDTenantUsageSnapshot = x.IDTenantUsageSnapshot,
                    TenantID = x.TenantID,
                    SnapshotDateUtc = x.SnapshotDateUtc,
                    DeviceCount = x.DeviceCount,
                    SensorDataRowCount = x.SensorDataRowCount,
                })
                .ToListAsync();
        }
    }
}
