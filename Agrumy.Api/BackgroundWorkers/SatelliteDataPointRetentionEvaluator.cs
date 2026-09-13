using Agrumy.Api.Dal.Interface;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Unlike SatelliteRasterRetentionEvaluator (which only clears the cached PNG, the grid/stats stay in the DB forever), this deletes the scene row itself - grid/stats included - once its own imagery date is past ServerConfig.SatelliteDataPointRetentionDays. Server-wide only, no per-tenant override; null/0 keeps every scene forever.
    public sealed class SatelliteDataPointRetentionEvaluator(ISatelliteSceneRepository sceneRepo, IServerConfigRepository serverConfigRepo, ILogger<SatelliteDataPointRetentionEvaluator> logger)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            var serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            if (serverConfig.SatelliteDataPointRetentionDays is not > 0)
            {
                return;
            }
            DateOnly cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-serverConfig.SatelliteDataPointRetentionDays.Value));
            int deleted = await sceneRepo.ScenesDeleteOlderThanAsync(cutoff);
            if (deleted > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Satellite data point retention: deleted {Count} scenes with SceneDateUtc older than {Cutoff}.", deleted, cutoff);
            }
        }
    }
}
