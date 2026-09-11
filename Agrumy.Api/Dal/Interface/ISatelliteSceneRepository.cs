using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// farmParcelZoneSatelliteScene/parcelSatelliteIndex storage (Detaljni dizajn S, D9/D10) - narrow facet, the job's write path and the read API's query path.
    public interface ISatelliteSceneRepository
    {
        Task<FarmParcelZoneSatelliteScene?> SceneGetBySourceIdAsync(int farmParcelZoneId, string sourceSceneId);

        Task<FarmParcelZoneSatelliteScene> SceneAddAsync(FarmParcelZoneSatelliteScene scene);

        Task<IList<FarmParcelZoneSatelliteScene>> ScenesGetAsync(int farmParcelZoneId, DateOnly? sinceUtc = null);

        Task<FarmParcelZoneSatelliteScene?> LatestSceneGetAsync(int farmParcelZoneId);

        /// S-C, D4 - the scene the date slider should show for this zone when its own history has no exact match for the selected date: the latest one at or before it. Null means the zone has nothing that old yet.
        Task<FarmParcelZoneSatelliteScene?> SceneAtOrBeforeDateAsync(int farmParcelZoneId, DateOnly date);

        /// S-C map's date slider - the union of scene dates across every zone in the current scope, one cheap query instead of fetching each zone's full history.
        Task<IList<DateOnly>> DistinctSceneDatesAsync(IReadOnlyCollection<int> farmParcelZoneIds);

        Task<ParcelSatelliteIndex> IndexUpsertAsync(ParcelSatelliteIndex index);

        Task<ParcelSatelliteIndex?> IndexGetAsync(int sceneId, SatelliteIndex index);

        Task<IList<ParcelSatelliteIndex>> IndicesGetForSceneAsync(int sceneId);

        /// D10 - fills in a previously-backfilled scene's grid once fetched on first view; stats/bounds stay whatever the backfill already wrote.
        Task IndexGridSetAsync(int idParcelSatelliteIndex, string gridBase64);

        /// D10 retention job - clears ImagePath for every index row past the cutoff so the next view regenerates from GridBase64; never touches GridBase64/StatsJson themselves.
        Task<int> ImagePathsClearOlderThanAsync(DateTimeOffset cutoffUtc);

        /// Time series across the scene's whole history for one zone+index, straight off the stats columns - no raster read (Detaljni dizajn S, B4).
        Task<IList<SatelliteSeriesPoint>> SeriesGetAsync(int farmParcelZoneId, SatelliteIndex index, DateOnly? fromUtc, DateOnly? toUtc, bool onlyReliable);

        Task FarmParcelZoneBackfillCompletedSetAsync(int farmParcelZoneId, DateTimeOffset completedAtUtc);
    }
}
