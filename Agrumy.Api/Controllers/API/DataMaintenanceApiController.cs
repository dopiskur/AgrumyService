using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// "Optimize Old Data" / "Purge Old Data", Global admin only (affects every tenant's telemetry) - both dispatch to BackgroundJobQueue and return 202 immediately instead of holding the request open for a large table's processing time.
    [Route("api/DataMaintenance")]
    [Authorize]
    public class DataMaintenanceApiController(
        IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, BackgroundJobQueue jobQueue, ILogger<DataMaintenanceApiController> logger)
        : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        [HttpPost("Optimize")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public ActionResult Optimize([FromBody] DataMaintenanceRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide data maintenance requires the Global admin role");
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
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public ActionResult Purge([FromBody] DataPurgeRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide data maintenance requires the Global admin role");
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
            jobQueue.Enqueue(async (services, ct) =>
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Purge Old Data started (older than {Days} days, cutoff {Cutoff:u}).", olderThanDays, cutoffUtc);
                }
                await services.GetRequiredService<ISensorDataRepository>().PurgeOldSensorDataAsync(cutoffUtc, ct);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Purge Old Data finished (cutoff {Cutoff:u}).", cutoffUtc);
                }
            });

            return Accepted();
        }

        /// Manual trigger for the same recycle-bin purge cycle PurgeOrphanedSensorDataBackgroundService runs on a schedule; forces PurgeOrphanedSensorDataEvaluator.RunOnceAsync regardless of PurgeOrphanedSensorDataScheduleEnabled, since an explicit admin click is not "the schedule". Marks anything newly past its tenant's retention AND reaps everything already Purged (including finalizing any "Delete permanently now"/"Empty Recycle Bin" actions instead of waiting for the next scheduled tick).
        [HttpPost("PurgeOrphaned")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public ActionResult PurgeOrphaned([FromBody] RecycleBinPurgeRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide data maintenance requires the Global admin role");
            }
            if (request.ConfirmationPhrase != RecycleBinPurgeRequest.RequiredPhrase)
            {
                return BadRequest($"Type \"{RecycleBinPurgeRequest.RequiredPhrase}\" to confirm this destructive action.");
            }

            jobQueue.Enqueue(async (services, ct) =>
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Recycle bin purge cycle started (manual trigger).");
                }
                await services.GetRequiredService<PurgeOrphanedSensorDataEvaluator>().RunOnceAsync(ct);
            });

            return Accepted();
        }
    }
}
