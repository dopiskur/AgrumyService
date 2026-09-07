using api.Dal.Interface;

namespace api.BackgroundWorkers
{
    /// Roadmap #409 - the scheduled half of "Purge orphaned sensor data" (the other half is the manual trigger on DataMaintenanceApiController.PurgeOrphaned); both call the same ISensorDataRepository.PurgeOrphanedSensorDataAsync.
    public sealed class PurgeOrphanedSensorDataEvaluator(IServerConfigRepository serverConfigRepo, ISensorDataRepository sensorDataRepo, ILogger<PurgeOrphanedSensorDataEvaluator> logger)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            var config = await serverConfigRepo.ServerConfigGetAsync(1);
            if (!config.PurgeOrphanedSensorDataScheduleEnabled)
            {
                return;
            }

            int retentionDays = config.RecycleBinRetentionDays ?? 30;
            if (retentionDays <= 0)
            {
                return;
            }

            long deleted = await sensorDataRepo.PurgeOrphanedSensorDataAsync(retentionDays, ct);
            if (deleted > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Scheduled orphaned sensor data purge deleted {Count} row(s) (retention {Days}d).", deleted, retentionDays);
            }
        }
    }
}
