using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Notifications;
using Agrumy.Shared.Security;
using Agrumy.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Server-wide settings, admin-only; there is exactly one row (id 1), auto-created on first read. Writes go through ServerConfigSectionsApiController - this controller only reads the whole row and hosts the non-section actions (tests, health, allowlists).
    [Route("api/ServerConfig")]
    public class ServerConfigApiController(IServerConfigRepository serverConfigRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, IEnumerable<INotificationChannel> notificationChannels, Agrumy.Api.Diagnostics.IServerHealthService serverHealthService, ISsrfAllowlistRepository ssrfAllowlistRepo) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        // These are SERVER-WIDE settings, so Global admin only.

        [HttpGet]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult<ServerConfig>> Get()
        {
            if (!CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader))
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }
            // Write-only, same "blank keeps existing" convention as TenantWifiConfig.Password - the repo returns the real (decrypted) values for internal senders like MqttCommandPublisher/EmailNotificationChannel to actually authenticate with, but this API boundary never echoes them back.
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            config.MqttPassword = null;
            config.EmailPassword = null;
            config.ArchivePassword = null;
            config.WebhookSecret = null;
            return Ok(config);
        }

        /// Sends a real test message through the saved (not the unsaved form's) Email settings - "Save" first, then test - so an admin can confirm SMTP actually works without waiting on a real alert/activation email.
        [HttpPost("TestEmail")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> TestEmail(string toEmail)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                return BadRequest("toEmail is required.");
            }

            INotificationChannel? email = notificationChannels.FirstOrDefault(c => c.Name == "email");
            if (email is null)
            {
                return StatusCode(500, "Email channel is not registered.");
            }

            var notification = new Notification(
                "Agrumy test email",
                "This is a test email from your Agrumy server's Server Settings -> Email section.",
                new NotificationRecipient(toEmail));
            NotificationResult result = await email.SendAsync(notification);
            return result.Sent ? Ok() : BadRequest(result.Detail ?? "Send failed.");
        }

        /// Sends a real test POST through the saved Webhook settings, same "Save first, then test" pattern as TestEmail above.
        [HttpPost("TestWebhook")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> TestWebhook()
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }

            INotificationChannel? webhook = notificationChannels.FirstOrDefault(c => c.Name == "webhook");
            if (webhook is null)
            {
                return StatusCode(500, "Webhook channel is not registered.");
            }
            if (!await webhook.IsConfiguredAsync())
            {
                return BadRequest("Webhook is not enabled, or its URL is missing/not https - save first.");
            }

            var notification = new Notification(
                "Agrumy test webhook",
                "This is a test notification from your Agrumy server's Server Settings -> Webhook section.",
                new NotificationRecipient());
            NotificationResult result = await webhook.SendAsync(notification);
            return result.Sent ? Ok() : BadRequest(result.Detail ?? "Send failed.");
        }

        /// Tests the UNSAVED form's archive DB credentials before SaveArchiveSettings ever persists them, so a bad host/port/password never silently disables archiving later. Password blank means "use whatever's already saved" (see ArchiveDbTestRequest's own remarks).
        [HttpPost("TestArchiveDatabase")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> TestArchiveDatabase([FromBody] ArchiveDbTestRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }
            if (string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.DatabaseName) || string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest("Host, database name, and username are required.");
            }
            if (request.Port is null or < 1 or > 65535)
            {
                return BadRequest("Port must be between 1 and 65535.");
            }

            string? password = request.Password;
            if (string.IsNullOrEmpty(password))
            {
                password = (await serverConfigRepo.ServerConfigGetAsync(1)).ArchivePassword;
                if (string.IsNullOrEmpty(password))
                {
                    return BadRequest("Password is required - no existing password is saved to fall back on.");
                }
            }

            (bool success, string? error) = await ArchiveDbConnectionTester.TestAsync(request.Host, request.Port.Value, request.DatabaseName, request.Username, password);
            return success ? Ok() : BadRequest(error);
        }

        /// The "Data Archiving" subsection's own self-contained save (Web's ServerConfigController.SaveArchiveSettings JS button, not the main Server Settings form), independent of every other tab. Tests the connection first when enabling (skipped when Enabled is false - "disable" needs no working credentials, the own explicit design decision), only THEN persists.
        [HttpPost("ArchiveSettings")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> SaveArchiveSettings([FromBody] ArchiveSettingsSaveRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }

            ServerConfig current = await serverConfigRepo.ServerConfigGetAsync(1);

            if (request.Enabled)
            {
                if (!Enum.IsDefined(request.CutoffMode))
                {
                    return BadRequest("Unknown archive cutoff mode: " + request.CutoffMode);
                }
                if (request.CutoffMode == ArchiveCutoffMode.CustomRollingDays && request.CustomRollingDays is not (90 or 180 or 365))
                {
                    return BadRequest("Custom rolling-window cutoff must be one of the preset options (90, 180, or 365 days).");
                }
                if (request.CutoffMode == ArchiveCutoffMode.CustomDate && request.CustomCutoffDate is null)
                {
                    return BadRequest("A custom cutoff date is required for that mode.");
                }
                if (string.IsNullOrWhiteSpace(request.Host) || string.IsNullOrWhiteSpace(request.DatabaseName) || string.IsNullOrWhiteSpace(request.Username))
                {
                    return BadRequest("Archive DB host, database name, and username are required to enable database archiving.");
                }
                if (request.Port is null or < 1 or > 65535)
                {
                    return BadRequest("Archive DB port must be between 1 and 65535.");
                }

                string? password = string.IsNullOrEmpty(request.Password) ? current.ArchivePassword : request.Password;
                if (string.IsNullOrEmpty(password))
                {
                    return BadRequest("Archive DB password is required.");
                }
                (bool success, string? error) = await ArchiveDbConnectionTester.TestAsync(request.Host, request.Port.Value, request.DatabaseName, request.Username, password);
                if (!success)
                {
                    return BadRequest(error);
                }
            }

            current.ArchiveEnabled = request.Enabled;
            current.ArchiveCutoffMode = request.CutoffMode;
            current.ArchiveCustomCutoffDate = request.CustomCutoffDate;
            current.ArchiveCustomRollingDays = request.CustomRollingDays;
            current.ArchiveHost = request.Host;
            current.ArchivePort = request.Port;
            current.ArchiveDatabaseName = request.DatabaseName;
            current.ArchiveUsername = request.Username;
            // Blank means "keep existing" - EfServerConfigRepository.ServerConfigUpdateAsync's own blank-keeps-existing handling applies here exactly as it does for a full ServerConfig save.
            current.ArchivePassword = request.Password;

            await serverConfigRepo.ServerConfigUpdateAsync(current);
            await WriteAuditAsync("ServerConfig.ArchiveSettingsUpdated", null, "ServerConfig", "1", null);
            return Ok();
        }

        /// Passive Server Health card, polled by the Web page on an interval (Agrumy.Web's ServerConfigController.Health -> live-refresh.js), not an on-demand test button like TestEmail/TestArchiveDatabase above. Only lists a dependency currently enabled/configured in ServerConfig - see ServerHealthService.
        [HttpGet("Health")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult<IReadOnlyList<ServerHealthEntry>>> GetHealth()
        {
            if (!CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader))
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }
            return Ok(await serverHealthService.GetStatusesAsync());
        }

        /// HttpFirmwareFetcher's own SsrfGuard exceptions (Firmware/Webhook keep separate lists - see SsrfGuard/ISsrfAllowlistRepository remarks).
        [HttpGet("FirmwareSsrfAllowlist")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult<IReadOnlyList<SsrfAllowlistEntry>>> GetFirmwareSsrfAllowlist() =>
            !CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader)
                ? ForbidWith("Server-wide settings require the Global admin role")
                : Ok(await ssrfAllowlistRepo.FirmwareAllowlistGetAllAsync());

        [HttpPost("FirmwareSsrfAllowlist")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult<SsrfAllowlistEntry>> AddFirmwareSsrfAllowlistEntry([FromBody] SsrfAllowlistEntry entry)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }
            string? error = await ValidateAllowlistEntryAsync(entry, await ssrfAllowlistRepo.FirmwareAllowlistGetAllAsync());
            if (error != null)
            {
                return BadRequest(error);
            }
            SsrfAllowlistEntry added = await ssrfAllowlistRepo.FirmwareAllowlistAddAsync(entry);
            await WriteAuditAsync("ServerConfig.FirmwareSsrfAllowlistEntryAdded", null, "FirmwareSsrfAllowlistEntry", added.Id.ToString(), null);
            return Ok(added);
        }

        [HttpDelete("FirmwareSsrfAllowlist/{id:int}")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> DeleteFirmwareSsrfAllowlistEntry(int id)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }
            await ssrfAllowlistRepo.FirmwareAllowlistDeleteAsync(id);
            await WriteAuditAsync("ServerConfig.FirmwareSsrfAllowlistEntryDeleted", null, "FirmwareSsrfAllowlistEntry", id.ToString(), null);
            return Ok();
        }

        [HttpGet("WebhookSsrfAllowlist")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult<IReadOnlyList<SsrfAllowlistEntry>>> GetWebhookSsrfAllowlist() =>
            !CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader)
                ? ForbidWith("Server-wide settings require the Global admin role")
                : Ok(await ssrfAllowlistRepo.WebhookAllowlistGetAllAsync());

        [HttpPost("WebhookSsrfAllowlist")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult<SsrfAllowlistEntry>> AddWebhookSsrfAllowlistEntry([FromBody] SsrfAllowlistEntry entry)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }
            string? error = await ValidateAllowlistEntryAsync(entry, await ssrfAllowlistRepo.WebhookAllowlistGetAllAsync());
            if (error != null)
            {
                return BadRequest(error);
            }
            SsrfAllowlistEntry added = await ssrfAllowlistRepo.WebhookAllowlistAddAsync(entry);
            await WriteAuditAsync("ServerConfig.WebhookSsrfAllowlistEntryAdded", null, "WebhookSsrfAllowlistEntry", added.Id.ToString(), null);
            return Ok(added);
        }

        [HttpDelete("WebhookSsrfAllowlist/{id:int}")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> DeleteWebhookSsrfAllowlistEntry(int id)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Server-wide settings require the Global admin role");
            }
            await ssrfAllowlistRepo.WebhookAllowlistDeleteAsync(id);
            await WriteAuditAsync("ServerConfig.WebhookSsrfAllowlistEntryDeleted", null, "WebhookSsrfAllowlistEntry", id.ToString(), null);
            return Ok();
        }

        /// Pattern must be a hostname (matched exactly against the request URI's Host, case-insensitive) or a CIDR range like "192.168.1.0/24" - SsrfGuard.IsCidrPattern decides which at request time, so validation here only rejects blank/duplicate/oversized input, not the two forms.
        private static Task<string?> ValidateAllowlistEntryAsync(SsrfAllowlistEntry entry, IReadOnlyList<SsrfAllowlistEntry> existing)
        {
            string pattern = entry.Pattern?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return Task.FromResult<string?>("Pattern is required.");
            }
            if (pattern.Length > 255)
            {
                return Task.FromResult<string?>("Pattern must be 255 characters or fewer.");
            }
            if (existing.Any(e => string.Equals(e.Pattern, pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult<string?>($"'{pattern}' is already on the allowlist.");
            }
            if (!entry.AllowPrivateNetwork && !entry.AllowInsecureHttp)
            {
                return Task.FromResult<string?>("At least one of Allow private network / Allow plain http must be checked - otherwise this entry does nothing.");
            }
            entry.Pattern = pattern;
            return Task.FromResult<string?>(null);
        }

        /// The Register page is anonymous and must not call the admin-only Get() above just to know whether to show a "create a new tenant" field - this exposes only that one flag.
        [HttpGet("Public")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicServerConfig>> GetPublic()
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            return Ok(new PublicServerConfig { AllowSelfServiceTenantCreation = config.AllowSelfServiceTenantCreation });
        }
    }
}
