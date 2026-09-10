using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// ITenantRepository members - forwarded to the standalone EfTenantRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<bool> TenantGetAsync(string tenantName) => tenantRepository.TenantGetAsync(tenantName);

        public Task<int?> TenantGetIdAsync(string tenantName) => tenantRepository.TenantGetIdAsync(tenantName);

        public Task<int> TenantAddAsync(string tenantName) => tenantRepository.TenantAddAsync(tenantName);

        public Task<IList<Tenant>> TenantsGetAllAsync() => tenantRepository.TenantsGetAllAsync();

        public Task<Tenant?> TenantGetByIdAsync(int idTenant) => tenantRepository.TenantGetByIdAsync(idTenant);

        public Task TenantUpdateAsync(Tenant tenant) => tenantRepository.TenantUpdateAsync(tenant);

        public Task<TenantQuota?> TenantQuotaGetAsync(int idTenant) => tenantRepository.TenantQuotaGetAsync(idTenant);

        public Task TenantQuotaSetAsync(TenantQuota quota) => tenantRepository.TenantQuotaSetAsync(quota);

        public Task TenantEmergencyStopSetAsync(int idTenant, bool active) => tenantRepository.TenantEmergencyStopSetAsync(idTenant, active);

        public Task<bool> TenantZeroIsEmptyAsync() => tenantRepository.TenantZeroIsEmptyAsync();

        public Task<IList<TenantWifiConfig>> TenantWifiConfigsGetAsync(int tenantID) => tenantRepository.TenantWifiConfigsGetAsync(tenantID);

        public Task<TenantWifiConfig> TenantWifiConfigAddAsync(TenantWifiConfig config) => tenantRepository.TenantWifiConfigAddAsync(config);

        public Task<TenantWifiConfig?> TenantWifiConfigGetByIdAsync(int idTenantWifiConfig) => tenantRepository.TenantWifiConfigGetByIdAsync(idTenantWifiConfig);

        public Task TenantWifiConfigUpdateAsync(TenantWifiConfig config) => tenantRepository.TenantWifiConfigUpdateAsync(config);

        public Task TenantWifiConfigDeleteAsync(int idTenantWifiConfig) => tenantRepository.TenantWifiConfigDeleteAsync(idTenantWifiConfig);

        public Task TenantUsageSnapshotRecordAsync(int idTenant, DateTimeOffset snapshotDateUtc) => tenantRepository.TenantUsageSnapshotRecordAsync(idTenant, snapshotDateUtc);

        public Task<IReadOnlyList<TenantUsageSnapshot>> TenantUsageSnapshotsGetAsync(int idTenant, int days) => tenantRepository.TenantUsageSnapshotsGetAsync(idTenant, days);
    }
}
