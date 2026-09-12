using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Per-tenant satellite module config CRUD (Detaljni dizajn S, D1/D8/D11/D12) - narrow facet, mirrors ITenantRepository's TenantWifiConfig* shape.
    public interface ISatelliteConfigRepository
    {
        Task<TenantSatelliteConfig?> SatelliteConfigGetAsync(int tenantId);

        /// Every tenant with Enabled=true and a config row - the daily job's own per-tenant loop.
        Task<IList<TenantSatelliteConfig>> SatelliteConfigsGetEnabledAsync();

        /// Insert-or-update by TenantID; blank ClientSecret keeps whatever is already stored (same convention as TenantWifiConfigUpdateAsync).
        Task<TenantSatelliteConfig> SatelliteConfigUpsertAsync(TenantSatelliteConfig config);

        /// Internal use only (CdseTokenProvider) - returns the real decrypted ClientSecret, never exposed through the API boundary.
        Task<(string? ClientId, string? ClientSecret)> SatelliteConfigCredentialsGetAsync(int tenantId);

        Task SatelliteConfigTokenIssuedAsync(int tenantId, DateTimeOffset issuedAtUtc);

        Task SatelliteConfigQuotaSnapshotSetAsync(int tenantId, string quotaSnapshotJson, DateTimeOffset? pausedUntilUtc);

        Task SatelliteConfigQuotaPausedNotifiedAsync(int tenantId, DateTimeOffset? notifiedAtUtc);

        /// The only writer of LastAutoSyncUtc - called exclusively by SatelliteSyncEvaluator's automatic (non-manual-trigger) path.
        Task SatelliteConfigLastAutoSyncSetAsync(int tenantId, DateTimeOffset lastAutoSyncUtc);
    }
}
