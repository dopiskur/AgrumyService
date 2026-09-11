using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Satellite;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Agrumy.Api.Dal
{
    internal sealed class EfSatelliteSceneRepository(AgrumyDbContext db) : ISatelliteSceneRepository
    {
        public async Task<FarmParcelZoneSatelliteScene?> SceneGetBySourceIdAsync(int farmParcelZoneId, string sourceSceneId)
        {
            var row = await db.FarmParcelZoneSatelliteScenes.AsNoTracking()
                .FirstOrDefaultAsync(s => s.FarmParcelZoneID == farmParcelZoneId && s.SourceSceneId == sourceSceneId);
            return row == null ? null : ToDtoScene(row);
        }

        public async Task<FarmParcelZoneSatelliteScene> SceneAddAsync(FarmParcelZoneSatelliteScene scene)
        {
            var row = new FarmParcelZoneSatelliteSceneRow
            {
                FarmParcelZoneID = scene.FarmParcelZoneID,
                SceneDateUtc = scene.SceneDateUtc,
                SourceSceneId = scene.SourceSceneId,
                CloudPercent = scene.CloudPercent,
                ValidPixelPercent = scene.ValidPixelPercent,
                Reliable = scene.Reliable,
                IngestedUtc = scene.IngestedUtc == default ? DateTimeOffset.UtcNow : scene.IngestedUtc,
            };
            db.FarmParcelZoneSatelliteScenes.Add(row);
            await db.SaveChangesAsync();
            return ToDtoScene(row);
        }

        public async Task<IList<FarmParcelZoneSatelliteScene>> ScenesGetAsync(int farmParcelZoneId, DateOnly? sinceUtc = null)
        {
            IQueryable<FarmParcelZoneSatelliteSceneRow> q = db.FarmParcelZoneSatelliteScenes.AsNoTracking().Where(s => s.FarmParcelZoneID == farmParcelZoneId);
            if (sinceUtc != null)
            {
                q = q.Where(s => s.SceneDateUtc >= sinceUtc);
            }
            var rows = await q.OrderByDescending(s => s.SceneDateUtc).ToListAsync();
            return rows.Select(ToDtoScene).ToList();
        }

        public async Task<FarmParcelZoneSatelliteScene?> LatestSceneGetAsync(int farmParcelZoneId)
        {
            var row = await db.FarmParcelZoneSatelliteScenes.AsNoTracking()
                .Where(s => s.FarmParcelZoneID == farmParcelZoneId)
                .OrderByDescending(s => s.SceneDateUtc)
                .FirstOrDefaultAsync();
            return row == null ? null : ToDtoScene(row);
        }

        public async Task<ParcelSatelliteIndex> IndexUpsertAsync(ParcelSatelliteIndex index)
        {
            var row = await db.ParcelSatelliteIndices.FirstOrDefaultAsync(i => i.SceneID == index.SceneID && i.Index == (int)index.Index);
            bool isNew = row == null;
            row ??= new ParcelSatelliteIndexRow { SceneID = index.SceneID, Index = (int)index.Index };
            row.GridBase64 = index.GridBase64 ?? row.GridBase64;
            row.BoundsJson = index.BoundsJson;
            row.StatsJson = index.StatsJson;
            if (isNew)
            {
                db.ParcelSatelliteIndices.Add(row);
            }
            await db.SaveChangesAsync();
            return ToDtoIndex(row);
        }

        public async Task<ParcelSatelliteIndex?> IndexGetAsync(int sceneId, SatelliteIndex index)
        {
            var row = await db.ParcelSatelliteIndices.AsNoTracking().FirstOrDefaultAsync(i => i.SceneID == sceneId && i.Index == (int)index);
            return row == null ? null : ToDtoIndex(row);
        }

        public async Task<IList<ParcelSatelliteIndex>> IndicesGetForSceneAsync(int sceneId)
        {
            var rows = await db.ParcelSatelliteIndices.AsNoTracking().Where(i => i.SceneID == sceneId).ToListAsync();
            return rows.Select(ToDtoIndex).ToList();
        }

        public async Task IndexGridSetAsync(int idParcelSatelliteIndex, string gridBase64) =>
            await db.ParcelSatelliteIndices.Where(i => i.IDParcelSatelliteIndex == idParcelSatelliteIndex).ExecuteUpdateAsync(set => set.SetProperty(i => i.GridBase64, gridBase64));

        public async Task<int> ImagePathsClearOlderThanAsync(DateTimeOffset cutoffUtc)
        {
            var sceneIds = await db.FarmParcelZoneSatelliteScenes.AsNoTracking()
                .Where(s => s.IngestedUtc < cutoffUtc)
                .Select(s => s.IDFarmParcelZoneSatelliteScene)
                .ToListAsync();
            if (sceneIds.Count == 0)
            {
                return 0;
            }
            return await db.ParcelSatelliteIndices
                .Where(i => sceneIds.Contains(i.SceneID) && i.ImagePath != null)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.ImagePath, (string?)null));
        }

        public async Task<IList<SatelliteSeriesPoint>> SeriesGetAsync(int farmParcelZoneId, SatelliteIndex index, DateOnly? fromUtc, DateOnly? toUtc, bool onlyReliable)
        {
            IQueryable<FarmParcelZoneSatelliteSceneRow> scenesQuery = db.FarmParcelZoneSatelliteScenes.AsNoTracking().Where(s => s.FarmParcelZoneID == farmParcelZoneId);
            if (fromUtc != null)
            {
                scenesQuery = scenesQuery.Where(s => s.SceneDateUtc >= fromUtc);
            }
            if (toUtc != null)
            {
                scenesQuery = scenesQuery.Where(s => s.SceneDateUtc <= toUtc);
            }
            if (onlyReliable)
            {
                scenesQuery = scenesQuery.Where(s => s.Reliable);
            }
            var scenes = await scenesQuery.OrderBy(s => s.SceneDateUtc).ToListAsync();
            var sceneIds = scenes.Select(s => s.IDFarmParcelZoneSatelliteScene).ToList();
            var indexRows = await db.ParcelSatelliteIndices.AsNoTracking()
                .Where(i => sceneIds.Contains(i.SceneID) && i.Index == (int)index)
                .ToListAsync();
            var byScene = indexRows.ToDictionary(i => i.SceneID);

            var result = new List<SatelliteSeriesPoint>();
            foreach (var scene in scenes)
            {
                if (!byScene.TryGetValue(scene.IDFarmParcelZoneSatelliteScene, out ParcelSatelliteIndexRow? indexRow) || string.IsNullOrEmpty(indexRow.StatsJson))
                {
                    continue;
                }
                var stats = System.Text.Json.JsonSerializer.Deserialize<SatelliteIndexStats>(indexRow.StatsJson);
                if (stats == null)
                {
                    continue;
                }
                result.Add(new SatelliteSeriesPoint
                {
                    SceneDateUtc = scene.SceneDateUtc,
                    Reliable = scene.Reliable,
                    Mean = stats.Mean,
                    Min = stats.Min,
                    Max = stats.Max,
                    StdDev = stats.StdDev,
                });
            }
            return result;
        }

        public async Task FarmParcelZoneBackfillCompletedSetAsync(int farmParcelZoneId, DateTimeOffset completedAtUtc) =>
            await db.FarmParcelZones.Where(z => z.IDFarmParcelZone == farmParcelZoneId).ExecuteUpdateAsync(set => set.SetProperty(z => z.SatelliteBackfillCompletedUtc, completedAtUtc));

        private static FarmParcelZoneSatelliteScene ToDtoScene(FarmParcelZoneSatelliteSceneRow s) => new()
        {
            IDFarmParcelZoneSatelliteScene = s.IDFarmParcelZoneSatelliteScene,
            FarmParcelZoneID = s.FarmParcelZoneID,
            SceneDateUtc = s.SceneDateUtc,
            SourceSceneId = s.SourceSceneId,
            CloudPercent = s.CloudPercent,
            ValidPixelPercent = s.ValidPixelPercent,
            Reliable = s.Reliable,
            IngestedUtc = s.IngestedUtc,
        };

        private static ParcelSatelliteIndex ToDtoIndex(ParcelSatelliteIndexRow i) => new()
        {
            IDParcelSatelliteIndex = i.IDParcelSatelliteIndex,
            SceneID = i.SceneID,
            Index = (SatelliteIndex)i.Index,
            GridBase64 = i.GridBase64,
            BoundsJson = i.BoundsJson,
            StatsJson = i.StatsJson,
        };
    }
}
