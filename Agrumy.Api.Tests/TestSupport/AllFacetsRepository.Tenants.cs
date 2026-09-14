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

        public Task<bool> TenantDeleteAsync(int idTenant, bool deleteUsers) => tenantRepository.TenantDeleteAsync(idTenant, deleteUsers);

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

        public Task<TenantAlertConfig> TenantAlertConfigGetAsync(int idTenant) => tenantRepository.TenantAlertConfigGetAsync(idTenant);

        public Task TenantAlertConfigUpdateAsync(int idTenant, TenantAlertConfig config) => tenantRepository.TenantAlertConfigUpdateAsync(idTenant, config);

        public Task<WeatherLocationState> WeatherLocationStateGetAsync(int idTenant, double latitude, double longitude) => tenantRepository.WeatherLocationStateGetAsync(idTenant, latitude, longitude);

        public Task WeatherLocationStateSetWeatherAsync(int idTenant, double latitude, double longitude, bool rainPredicted, DateTimeOffset checkedAtUtc) =>
            tenantRepository.WeatherLocationStateSetWeatherAsync(idTenant, latitude, longitude, rainPredicted, checkedAtUtc);

        public Task WeatherLocationStateSetFrostAsync(int idTenant, double latitude, double longitude, bool frostPredicted, int? hoursAhead, DateTimeOffset checkedAtUtc) =>
            tenantRepository.WeatherLocationStateSetFrostAsync(idTenant, latitude, longitude, frostPredicted, hoursAhead, checkedAtUtc);

        public Task WeatherLocationStateSetOutdoorAsync(int idTenant, double latitude, double longitude, double? temperatureC, double? humidityPercent, double? windSpeedMetersPerSecond, double? pressureHpa, DateTimeOffset checkedAtUtc) =>
            tenantRepository.WeatherLocationStateSetOutdoorAsync(idTenant, latitude, longitude, temperatureC, humidityPercent, windSpeedMetersPerSecond, pressureHpa, checkedAtUtc);
    }
}
