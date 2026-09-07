using api.BackgroundWorkers;
using api.Dal;
using api.Dal.Interface;
using api.Models;
using api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Controllers.API
{
    /// "Optimize Old Data" / "Purge Old Data", Global admin only (affects every tenant's telemetry) - both dispatch to BackgroundJobQueue and return 202 immediately instead of holding the request open for a large table's processing time.
    [Route("api/DataMaintenance")]
    [Authorize(Roles = RoleNames.GlobalAdmin)]
    public class DataMaintenanceApiController(
        IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, AgrumyDbContext db, BackgroundJobQueue jobQueue, ILogger<DataMaintenanceApiController> logger, IServerConfigRepository serverConfigRepo)
        : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        /// Lets Agrumy.Web decide whether to show the MariaDB-only "shrink files on disk?" dialog before confirming a Purge - Postgres/TimescaleDB reclaims disk space automatically.
        [HttpGet("Provider")]
        public ActionResult<DataMaintenanceProviderInfo> GetProvider()
        {
            if (!CallerIsGlobalAdmin)
            {
                return StatusCode(403, "Server-wide data maintenance requires the Global admin role");
            }
            return Ok(new DataMaintenanceProviderInfo { IsMySql = !db.Database.IsNpgsql() });
        }

        [HttpPost("Optimize")]
        public ActionResult Optimize([FromBody] DataMaintenanceRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return StatusCode(403, "Server-wide data maintenance requires the Global admin role");
            }
            if (!DataMaintenanceThresholds.AllowedDays.Contains(request.OlderThanDays))
            {
                return BadRequest("Unsupported threshold.");
            }

            DateTime cutoffUtc = DateTime.UtcNow.AddDays(-request.OlderThanDays);
            int olderThanDays = request.OlderThanDays;
            jobQueue.Enqueue(async (services, ct) =>
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Optimize Old Data started (older than {Days} days, cutoff {Cutoff:u}).", olderThanDays, cutoffUtc);
                }
                await services.GetRequiredService<ISensorDataRepository>().OptimizeOldSensorDataAsync(cutoffUtc, ct);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Optimize Old Data finished (cutoff {Cutoff:u}).", cutoffUtc);
                }
            });

            return Accepted();
        }

        [HttpPost("Purge")]
        public ActionResult Purge([FromBody] DataPurgeRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return StatusCode(403, "Server-wide data maintenance requires the Global admin role");
            }
            if (!DataMaintenanceThresholds.AllowedDays.Contains(request.OlderThanDays))
            {
                return BadRequest("Unsupported threshold.");
            }
            // Enforced here too, not just in the Web form - the API is reachable directly.
            if (request.ConfirmationPhrase != DataPurgeRequest.RequiredPhrase)
            {
                return BadRequest($"Type \"{DataPurgeRequest.RequiredPhrase}\" to confirm this destructive action.");
            }

            DateTime cutoffUtc = DateTime.UtcNow.AddDays(-request.OlderThanDays);
            int olderThanDays = request.OlderThanDays;
            bool shrinkAfterPurge = request.ShrinkAfterPurge;
            jobQueue.Enqueue(async (services, ct) =>
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Purge Old Data started (older than {Days} days, cutoff {Cutoff:u}, shrink={Shrink}).",
                        olderThanDays, cutoffUtc, shrinkAfterPurge);
                }
                await services.GetRequiredService<ISensorDataRepository>().PurgeOldSensorDataAsync(cutoffUtc, shrinkAfterPurge, ct);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Purge Old Data finished (cutoff {Cutoff:u}).", cutoffUtc);
                }
            });

            return Accepted();
        }

        /// Roadmap #409 - manual trigger for "Purge orphaned sensor data" (the recycle bin's SensorData cleanup). Cutoff is always serverConfig.RecycleBinRetentionDays, not caller-chosen - a device only ever qualifies once it's already left the Recycle Bin listing.
        [HttpPost("PurgeOrphaned")]
        public async Task<ActionResult> PurgeOrphaned([FromBody] DataPurgeOrphanedRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return StatusCode(403, "Server-wide data maintenance requires the Global admin role");
            }
            if (request.ConfirmationPhrase != DataPurgeOrphanedRequest.RequiredPhrase)
            {
                return BadRequest($"Type \"{DataPurgeOrphanedRequest.RequiredPhrase}\" to confirm this destructive action.");
            }

            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            int retentionDays = config.RecycleBinRetentionDays ?? 30;
            if (retentionDays <= 0)
            {
                return BadRequest("Recycle bin retention is 0 - there is nothing to purge.");
            }

            jobQueue.Enqueue(async (services, ct) =>
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Purge Orphaned Sensor Data started (retention {Days}d).", retentionDays);
                }
                long deleted = await services.GetRequiredService<ISensorDataRepository>().PurgeOrphanedSensorDataAsync(retentionDays, ct);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Purge Orphaned Sensor Data finished ({Count} row(s) deleted).", deleted);
                }
            });

            return Accepted();
        }
    }
}
