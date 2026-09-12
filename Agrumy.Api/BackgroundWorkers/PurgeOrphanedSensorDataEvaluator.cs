using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.BackgroundWorkers
{
    /// The recycle bin's two-phase purge cycle: (1) mark any Deleted-but-not-yet-Purged device/farm whose OWNING TENANT's retention has elapsed, (2) reap everything currently Purged - the actual, irreversible removal (SensorData included). The manual "Delete permanently now"/"Empty Recycle Bin" actions only ever do phase 1 for their target(s) directly; this evaluator is what actually finishes the job, on a schedule or via the manual "Purge Orphaned Sensor Data" trigger (DataMaintenanceApiController.PurgeOrphaned) forcing RunOnceAsync regardless of the schedule toggle.
    public sealed class PurgeOrphanedSensorDataEvaluator(
        IServerConfigRepository serverConfigRepo, IDeviceRepository deviceRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, ILogger<PurgeOrphanedSensorDataEvaluator> logger)
    {
        /// The scheduled entry point - respects PurgeOrphanedSensorDataScheduleEnabled, unlike RunOnceAsync.
        public async Task RunScheduledAsync(CancellationToken ct = default)
        {
            var config = await serverConfigRepo.ServerConfigGetAsync(1);
            if (!config.PurgeOrphanedSensorDataScheduleEnabled)
            {
                return;
            }
            await RunOnceAsync(ct);
        }

        /// Runs the full mark+reap cycle unconditionally - the manual trigger's forced "run it right now" path.
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            int serverDefaultRetentionDays = config.RecycleBinRetentionDays ?? 30;

            int devicesMarked = await deviceRepo.DeviceRecycleBinMarkPurgedByRetentionAsync(serverDefaultRetentionDays, ct);
            int farmsMarked = await deviceFarmUnitRepo.DeviceFarmRecycleBinMarkPurgedByRetentionAsync(serverDefaultRetentionDays, ct);

            // Farms first - reaping a farm already reaps every device its own Purged cascade covers, so the device pass below only picks up whatever's left (standalone devices, or devices marked independently of any farm).
            int farmsReaped = 0;
            foreach (var (idDeviceFarm, tenantId) in await deviceFarmUnitRepo.DeviceFarmPurgedIdsGetAsync())
            {
                if (await deviceFarmUnitRepo.DeviceFarmRecycleBinPurgeAsync(idDeviceFarm, tenantId))
                {
                    farmsReaped++;
                }
            }
            int devicesReaped = 0;
            foreach (var (idDevice, tenantId) in await deviceRepo.DevicePurgedIdsGetAsync())
            {
                if (await deviceRepo.DeviceRecycleBinPurgeAsync(idDevice, tenantId))
                {
                    devicesReaped++;
                }
            }

            if (logger.IsEnabled(LogLevel.Information) && (devicesMarked + farmsMarked + devicesReaped + farmsReaped) > 0)
            {
                logger.LogInformation(
                    "Recycle bin purge cycle: marked {DevicesMarked} device(s)/{FarmsMarked} farm(s) for purge, permanently removed {DevicesReaped} device(s)/{FarmsReaped} farm(s).",
                    devicesMarked, farmsMarked, devicesReaped, farmsReaped);
            }
        }
    }
}
