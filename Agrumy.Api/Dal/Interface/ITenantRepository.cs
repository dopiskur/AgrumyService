using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Tenant facet - lookups, the silent create-on-registration path (see UserApiController.UserRegistration), and Tenant Management CRUD.
    public interface ITenantRepository
    {
        Task<bool> TenantGetAsync(string tenantName);
        Task<int?> TenantGetIdAsync(string tenantName);
        Task<int> TenantAddAsync(string tenantName);

        Task<IList<Tenant>> TenantsGetAllAsync();
        Task<Tenant?> TenantGetByIdAsync(int idTenant);
        Task TenantUpdateAsync(Tenant tenant);

        /// Deletes the tenant row itself plus its WifiConfigs/Quota/UsageSnapshots (the latter two cascade at the DB level already); its users either get deleted too (deleteUsers) or survive with TenantID set to null ("Unassigned" - see UserRow.TenantID). Caller (TenantApiController) is responsible for the "tenant still has devices" guard - this method assumes that check already passed. False if idTenant did not exist.
        Task<bool> TenantDeleteAsync(int idTenant, bool deleteUsers);

        /// Null for IDTenant=0 (the default/bootstrap tenant is always exempt); otherwise the tenant's own configured row, or TenantQuota.Default when none exists yet - never "unlimited" for a real tenant just because nobody has configured it.
        Task<TenantQuota?> TenantQuotaGetAsync(int idTenant);

        /// Upsert - Global Admin only, enforced by the caller (TenantQuotaApiController), not here.
        Task TenantQuotaSetAsync(TenantQuota quota);

        /// The only writer of Tenant.EmergencyStopActive (roadmap #230) - also bumps ConfigVersion for every device in the tenant so the change reaches them on their next poll rather than waiting for the heartbeat window.
        Task TenantEmergencyStopSetAsync(int idTenant, bool active);

        /// True only when TenantID=0 has no devices and at most the single still-unclaimed bootstrap admin row (see EfRepository.SeedBootstrapAdminAsync) - any real device or claimed user means ImportAsSentinel must refuse rather than overwrite them.
        Task<bool> TenantZeroIsEmptyAsync();

        /// Every saved WiFi AP for this tenant - DiscoveryApiController.Register's 0/1/many branching decides what to do based on this count.
        Task<IList<TenantWifiConfig>> TenantWifiConfigsGetAsync(int tenantID);

        Task<TenantWifiConfig> TenantWifiConfigAddAsync(TenantWifiConfig config);

        /// No tenant filter - for ownership checks before an authorized write, same pattern as DeviceGetByIdAsync.
        Task<TenantWifiConfig?> TenantWifiConfigGetByIdAsync(int idTenantWifiConfig);

        Task TenantWifiConfigUpdateAsync(TenantWifiConfig config);

        /// A no-op if the id does not exist.
        Task TenantWifiConfigDeleteAsync(int idTenantWifiConfig);

        /// Upserts today's device/sensorData counts for this tenant - called once daily by TenantUsageSnapshotEvaluator; a same-day re-run (e.g. a service restart) refreshes the existing row rather than duplicating it.
        Task TenantUsageSnapshotRecordAsync(int idTenant, DateTimeOffset snapshotDateUtc);

        /// Most recent snapshots first, capped at <paramref name="days"/> rows.
        Task<IReadOnlyList<TenantUsageSnapshot>> TenantUsageSnapshotsGetAsync(int idTenant, int days);

        /// Null-only object (every field null) when the tenant has no overrides set yet - never null itself, so a caller doesn't need a separate "not configured" branch.
        Task<TenantAlertConfig> TenantAlertConfigGetAsync(int idTenant);

        Task TenantAlertConfigUpdateAsync(int idTenant, TenantAlertConfig config);
    }
}
