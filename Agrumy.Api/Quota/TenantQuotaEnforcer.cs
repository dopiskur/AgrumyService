using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Quota
{
    /// Hard-block guard for TenantQuota - every check returns null when allowed, else the exact message the caller surfaces; ingest-volume limits only, never a feature gate (rule engine/notifications/dashboard/sensor catalog stay fully open regardless of quota).
    public sealed class TenantQuotaEnforcer(ITenantRepository tenantRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IUserRepository userRepo, ISimulationRepository simulationRepo)
    {
        public const string LimitMessage = "Limit for the current tier reached, please contact support.";

        /// IDTenant=0 (the default/bootstrap tenant) is exempt without ever reaching the repository - a pure business rule, not something that needs a DB round trip.
        private Task<TenantQuota?> GetQuotaAsync(int? tenantId) =>
            tenantId is int id and not 0 ? tenantRepo.TenantQuotaGetAsync(id) : Task.FromResult<TenantQuota?>(null);

        public async Task<string?> CheckCanAddFarmAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null)
            {
                return null;
            }
            int current = (await deviceFarmUnitRepo.DeviceFarmsGetAsync(tenantId)).Count;
            return current >= quota.MaxFarms ? LimitMessage : null;
        }

        public async Task<string?> CheckCanAddUnitAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null)
            {
                return null;
            }
            int current = (await deviceFarmUnitRepo.DeviceFarmUnitsGetAsync(tenantId)).Count;
            return current >= quota.MaxUnits ? LimitMessage : null;
        }

        /// Zones aren't queryable tenant-wide directly (DeviceFarmUnitZonesGetAsync takes one unit) - summed across the tenant's units instead, bounded by MaxUnits so this stays cheap.
        public async Task<string?> CheckCanAddZoneAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null)
            {
                return null;
            }
            IList<DeviceFarmUnit> units = await deviceFarmUnitRepo.DeviceFarmUnitsGetAsync(tenantId);
            int current = 0;
            foreach (DeviceFarmUnit unit in units)
            {
                current += (await deviceFarmUnitRepo.DeviceFarmUnitZonesGetAsync(unit.IDDeviceFarmUnit!.Value)).Count;
            }
            return current >= quota.MaxZones ? LimitMessage : null;
        }

        public async Task<string?> CheckControllerRelayCountAsync(int? tenantId, int relayCount)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            return quota != null && relayCount > quota.MaxControllersPerDevice ? LimitMessage : null;
        }

        public async Task<string?> CheckSensorFieldCountAsync(int? tenantId, int enabledSensorCount)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            return quota != null && enabledSensorCount > quota.MaxSensorsPerDevice ? LimitMessage : null;
        }

        public async Task<string?> CheckLoRaAllowedAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            return quota != null && !quota.LoRaEnabled ? LimitMessage : null;
        }

        public async Task<string?> CheckMinSensorIntervalAsync(int? tenantId, int? sleepSeconds)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null || sleepSeconds is not int seconds)
            {
                return null;
            }
            return seconds < quota.MinSensorIntervalMinutes * 60 ? LimitMessage : null;
        }

        public async Task<bool> IsMqttAllowedAsync(int? tenantId) => (await GetQuotaAsync(tenantId)) is null or { MqttEnabled: true };

        public async Task<bool> IsGatewayAllowedAsync(int? tenantId) => (await GetQuotaAsync(tenantId)) is null or { GatewayEnabled: true };

        public async Task<string?> CheckCanAddUserAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null)
            {
                return null;
            }
            int current = (await userRepo.UsersGetAsync(tenantId)).Count;
            return current >= quota.MaxUsers ? LimitMessage : null;
        }

        public async Task<string?> CheckCanAddSimulationAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null)
            {
                return null;
            }
            int current = (await simulationRepo.SimulationSessionsGetAsync(tenantId)).Count;
            return current >= quota.MaxSimulations ? LimitMessage : null;
        }
    }
}
