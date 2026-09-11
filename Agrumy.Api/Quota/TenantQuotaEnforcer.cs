using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Quota
{
    /// Hard-block guard for TenantQuota - every check returns null when allowed, else the exact message the caller surfaces; ingest-volume limits only, never a feature gate (rule engine/notifications/dashboard/sensor catalog stay fully open regardless of quota). A count-based check here is only race-free if the caller runs it inside the SAME Serializable transaction as the resource's own insert - see QuotaGuard for that shared, reusable shape, used by every repository Add method a TenantQuota check gates.
    public sealed class TenantQuotaEnforcer(ITenantRepository tenantRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IFarmOpenfieldRepository farmOpenfieldRepo, ISowingRepository sowingRepo, IFarmParcelRepository farmParcelRepo, IUserRepository userRepo, ISimulationRepository simulationRepo, IDeviceRepository deviceRepo)
    {
        public const string LimitMessage = "Limit for the current tier reached, please contact support.";

        /// IDTenant=0 (the default/bootstrap tenant) is exempt without ever reaching the repository - a pure business rule, not something that needs a DB round trip.
        private Task<TenantQuota?> GetQuotaAsync(int? tenantId) =>
            tenantId is int id and not 0 ? tenantRepo.TenantQuotaGetAsync(id) : Task.FromResult<TenantQuota?>(null);

        /// Checked at Register, the one real device-creation choke point (Discovery provisioning only queues a ProvisionDevice command consumed by that same call) - the only quota that directly costs telemetry rows/broker connections/LoRa slots.
        public async Task<string?> CheckCanAddDeviceAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null)
            {
                return null;
            }
            int current = (await deviceRepo.DevicesGetAsync(tenantId)).Count;
            return current >= quota.MaxDevices ? LimitMessage : null;
        }

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

        public async Task<string?> CheckCanAddCropAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null)
            {
                return null;
            }
            int current = (await sowingRepo.SowingsGetAsync(tenantId)).Count;
            return current >= quota.MaxCrops ? LimitMessage : null;
        }

        /// Same "not queryable tenant-wide, summed across the tenant's parcels instead" shape as CheckCanAddZoneAsync, bounded by the tenant's small admin-managed FarmOpenfield/FarmParcel counts.
        public async Task<string?> CheckCanAddParcelAsync(int? tenantId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null)
            {
                return null;
            }
            int current = 0;
            foreach (FarmOpenfield openfield in await farmOpenfieldRepo.FarmOpenfieldsGetAsync(tenantId))
            {
                foreach (FarmParcel parcel in await farmParcelRepo.FarmParcelsGetAsync(openfield.IDFarmOpenfield!.Value))
                {
                    current += (await farmParcelRepo.FarmParcelZonesGetAsync(parcel.IDFarmParcel!.Value)).Count;
                }
            }
            return current >= quota.MaxParcels ? LimitMessage : null;
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

        /// Separate from CheckMinSensorIntervalAsync above - that one only gates the CONFIGURED sleepSeconds value (an admin trying to set too short an interval), it does nothing to stop a device that just ignores its own configured interval and pushes telemetry faster anyway. This checks the ACTUAL elapsed time since the device's last accepted push.
        public async Task<string?> CheckSensorPushIntervalAsync(int? tenantId, int deviceId)
        {
            TenantQuota? quota = await GetQuotaAsync(tenantId);
            if (quota == null || quota.MinSensorIntervalMinutes <= 0)
            {
                return null;
            }
            bool allowed = await deviceRepo.DeviceCheckAndRecordSensorPushAsync(deviceId, TimeSpan.FromMinutes(quota.MinSensorIntervalMinutes));
            return allowed ? null : LimitMessage;
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

    /// Thrown by Agrumy.Api.Quota.QuotaGuard.RunAsync (or RegisterUserAsync's own inlined equivalent) when a quota check blocks the insert - same SsrfBlockedException-style "throw at the source, catch at the controller" shape used elsewhere in this codebase.
    public sealed class QuotaLimitExceededException(string message) : Exception(message);
}
