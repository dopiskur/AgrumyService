using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// ISatelliteConfigRepository/ISatelliteSceneRepository members - forwarded to the standalone Ef*Repository instances, same pattern as AllFacetsRepository.FarmOpenfield.cs.
    internal sealed partial class AllFacetsRepository
    {
        // ---- ISatelliteConfigRepository ----

        public Task<TenantSatelliteConfig?> SatelliteConfigGetAsync(int tenantId) => satelliteConfigRepository.SatelliteConfigGetAsync(tenantId);

        public Task<IList<TenantSatelliteConfig>> SatelliteConfigsGetEnabledAsync() => satelliteConfigRepository.SatelliteConfigsGetEnabledAsync();

        public Task<TenantSatelliteConfig> SatelliteConfigUpsertAsync(TenantSatelliteConfig config) => satelliteConfigRepository.SatelliteConfigUpsertAsync(config);

        public Task<(string? ClientId, string? ClientSecret)> SatelliteConfigCredentialsGetAsync(int tenantId) => satelliteConfigRepository.SatelliteConfigCredentialsGetAsync(tenantId);

        public Task SatelliteConfigTokenIssuedAsync(int tenantId, DateTimeOffset issuedAtUtc) => satelliteConfigRepository.SatelliteConfigTokenIssuedAsync(tenantId, issuedAtUtc);

        public Task SatelliteConfigQuotaSnapshotSetAsync(int tenantId, string quotaSnapshotJson, DateTimeOffset? pausedUntilUtc) => satelliteConfigRepository.SatelliteConfigQuotaSnapshotSetAsync(tenantId, quotaSnapshotJson, pausedUntilUtc);

        public Task SatelliteConfigQuotaPausedNotifiedAsync(int tenantId, DateTimeOffset? notifiedAtUtc) => satelliteConfigRepository.SatelliteConfigQuotaPausedNotifiedAsync(tenantId, notifiedAtUtc);

        // ---- ISatelliteSceneRepository ----

        public Task<FarmParcelZoneSatelliteScene?> SceneGetBySourceIdAsync(int farmParcelZoneId, string sourceSceneId) => satelliteSceneRepository.SceneGetBySourceIdAsync(farmParcelZoneId, sourceSceneId);

        public Task<FarmParcelZoneSatelliteScene> SceneAddAsync(FarmParcelZoneSatelliteScene scene) => satelliteSceneRepository.SceneAddAsync(scene);

        public Task<IList<FarmParcelZoneSatelliteScene>> ScenesGetAsync(int farmParcelZoneId, DateOnly? sinceUtc = null) => satelliteSceneRepository.ScenesGetAsync(farmParcelZoneId, sinceUtc);

        public Task<FarmParcelZoneSatelliteScene?> LatestSceneGetAsync(int farmParcelZoneId) => satelliteSceneRepository.LatestSceneGetAsync(farmParcelZoneId);

        public Task<ParcelSatelliteIndex> IndexUpsertAsync(ParcelSatelliteIndex index) => satelliteSceneRepository.IndexUpsertAsync(index);

        public Task<ParcelSatelliteIndex?> IndexGetAsync(int sceneId, SatelliteIndex index) => satelliteSceneRepository.IndexGetAsync(sceneId, index);

        public Task<IList<ParcelSatelliteIndex>> IndicesGetForSceneAsync(int sceneId) => satelliteSceneRepository.IndicesGetForSceneAsync(sceneId);

        public Task IndexGridSetAsync(int idParcelSatelliteIndex, string gridBase64) => satelliteSceneRepository.IndexGridSetAsync(idParcelSatelliteIndex, gridBase64);

        public Task<int> ImagePathsClearOlderThanAsync(DateTimeOffset cutoffUtc) => satelliteSceneRepository.ImagePathsClearOlderThanAsync(cutoffUtc);

        public Task<IList<SatelliteSeriesPoint>> SeriesGetAsync(int farmParcelZoneId, SatelliteIndex index, DateOnly? fromUtc, DateOnly? toUtc, bool onlyReliable) => satelliteSceneRepository.SeriesGetAsync(farmParcelZoneId, index, fromUtc, toUtc, onlyReliable);

        public Task FarmParcelZoneBackfillCompletedSetAsync(int farmParcelZoneId, DateTimeOffset completedAtUtc) => satelliteSceneRepository.FarmParcelZoneBackfillCompletedSetAsync(farmParcelZoneId, completedAtUtc);
    }
}
