using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Notifications;
using Agrumy.Shared.Security;
using Agrumy.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Server-wide settings, admin-only; there is exactly one row (id 1), auto-created on first read.
    [Route("api/ServerConfig")]
    public class ServerConfigApiController(IServerConfigRepository serverConfigRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, IEnumerable<INotificationChannel> notificationChannels, Agrumy.Api.Diagnostics.IServerHealthService serverHealthService) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        // These are SERVER-WIDE settings, so Global admin only.

        [HttpGet]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult<ServerConfig>> Get()
        {
            if (!CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader))
            {
                return StatusCode(403, "Server-wide settings require the Global admin role");
            }
            // Write-only, same "blank keeps existing" convention as TenantWifiConfig.Password - the repo returns the real (decrypted) values for internal senders like MqttCommandPublisher/EmailNotificationChannel to actually authenticate with, but this API boundary never echoes them back.
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            config.MqttPassword = null;
            config.EmailPassword = null;
            config.ArchivePassword = null;
            config.WebhookSecret = null;
            return Ok(config);
        }

        [HttpPut]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> Update([FromBody] ServerConfig config)
        {
            if (!CallerIsGlobalAdmin)
            {
                return StatusCode(403, "Server-wide settings require the Global admin role");
            }

            // A Custom source with no manifest URL (or a non-http one) would leave every sync failing with a vague error.
            if (!Enum.IsDefined(config.FirmwareSource))
            {
                return BadRequest("Unknown firmware source: " + config.FirmwareSource);
            }
            if (config.FirmwareSource == FirmwareSource.Custom &&
                (!Uri.TryCreate(config.FirmwareCustomRepositoryUrl, UriKind.Absolute, out Uri? customUri) || customUri.Scheme is not ("http" or "https")))
            {
                return BadRequest("Custom firmware source needs an absolute http(s) manifest URL.");
            }
            // Blank = back to the appsettings seed (EfRepository.ServerConfig's ToDto fallback); a value must be owner/name.
            config.FirmwareGitHubRepository = string.IsNullOrWhiteSpace(config.FirmwareGitHubRepository) ? null : config.FirmwareGitHubRepository.Trim().Trim('/');
            if (config.FirmwareGitHubRepository != null && config.FirmwareGitHubRepository.Count(c => c == '/') != 1)
            {
                return BadRequest("GitHub repository must be in owner/name form.");
            }

            // The server-wide default pair new devices are seeded with - same bound DeviceApiController.DeviceConfigControllerUpdate enforces on a per-device override.
            if (!SafetyLimitValidation.IsValid(config.WaterPumpMaxRunSeconds))
            {
                return BadRequest($"WaterPump max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }
            if (!SafetyLimitValidation.IsValid(config.WaterPumpCooldownSeconds))
            {
                return BadRequest($"WaterPump cooldown must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.");
            }

            // Hard ceiling matches AgrumyFirmware DeviceModel.h's MAX_RULES - above it the firmware silently drops extra rules.
            if (config.MaxRulesPerZone is < 1 or > 32)
            {
                return BadRequest("Max rules per zone must be between 1 and 32.");
            }

            // Negative retention is meaningless; 0/null are both valid ways to say "no automatic retention".
            if (config.SensorDataRetentionDays is < 0)
            {
                return BadRequest("Sensor data retention days must be 0/empty (disabled) or a positive number.");
            }

            // lat/lon are a pair - one set without the other silently sends WeatherEvaluator queries to (lat, 0) or (0, lon).
            if (config.WeatherLocationLat.HasValue != config.WeatherLocationLon.HasValue)
            {
                return BadRequest("Weather latitude and longitude must both be set, or both left empty.");
            }
            if (config.WeatherLocationLat is < -90 or > 90)
            {
                return BadRequest("Weather latitude must be between -90 and 90.");
            }
            if (config.WeatherLocationLon is < -180 or > 180)
            {
                return BadRequest("Weather longitude must be between -180 and 180.");
            }
            if (config.WeatherPollIntervalMinutes is < 1)
            {
                return BadRequest("Weather poll interval must be at least 1 minute.");
            }
            if (config.WeatherRainSkipThreshold is < 0 or > 100)
            {
                return BadRequest("Weather rain-skip threshold must be between 0 and 100 percent.");
            }

            // Negative is meaningless; 0/null both mean "auto-refresh disabled".
            if (config.FirmwareRefreshIntervalHours is < 0)
            {
                return BadRequest("Firmware auto-refresh interval must be 0/empty (disabled) or a positive number of hours.");
            }

            // Matches the 10s/5min bounds the Gateway design settled on - short enough a LoRa device's own retry loop stays reasonable, long enough to actually batch.
            if (config.GatewayWaitWindowSeconds is < 10 or > 300)
            {
                return BadRequest("Gateway wait window must be between 10 and 300 seconds.");
            }
            if (!Enum.IsDefined(config.GatewayMode))
            {
                return BadRequest("Unknown gateway mode: " + config.GatewayMode);
            }

            // Fixed dropdown on the Server Settings page - anything else means a stale/tampered form post.
            if (config.ProblemEventExpiryHours is not (1 or 6 or 12 or 24 or 48))
            {
                return BadRequest("Problem alert expiry must be one of 1, 6, 12, 24, or 48 hours.");
            }

            if (config.PasswordMinLength is < 4 or > 128)
            {
                return BadRequest("Minimum password length must be between 4 and 128.");
            }

            // 0 disables it; otherwise bounded to a week, same "0/off or a sane positive range" pattern as FirmwareRefreshIntervalHours.
            if (config.ConfigHeartbeatHours is < 0 or > 168)
            {
                return BadRequest("Config heartbeat must be 0 (disabled) or between 1 and 168 hours.");
            }

            if (config.MqttBrokerPort is < 1 or > 65535)
            {
                return BadRequest("MQTT broker port must be between 1 and 65535.");
            }
            if (config.MqttTransportEnabled && string.IsNullOrWhiteSpace(config.MqttBrokerHost))
            {
                return BadRequest("MQTT broker host is required to enable MQTT instant command push.");
            }

            if (config.EmailPort is < 1 or > 65535)
            {
                return BadRequest("SMTP port must be between 1 and 65535.");
            }
            if (config.EmailEnabled && (string.IsNullOrWhiteSpace(config.EmailHost) || string.IsNullOrWhiteSpace(config.EmailFromAddress)))
            {
                return BadRequest("SMTP host and From address are required to enable email notifications.");
            }

            // Fixed dropdown on the Server Settings page (5/15min, 1/2/6/12/24h) - a leaked PIN's blast radius should be an admin-chosen preset, not a free-text value.
            if (config.DevicePinValidMinutes is not (5 or 15 or 60 or 120 or 360 or 720 or 1440))
            {
                return BadRequest("Registration PIN validity must be one of the preset options.");
            }

            // Roadmap #209 - archiving is MariaDB/MySQL only (Postgres/TimescaleDB has its own native tiered storage, #14); enabling it on Postgres would just silently no-op in SensorDataArchiveEvaluator, better to say so now.
            if (config.ArchiveEnabled)
            {
                if (!Enum.IsDefined(config.ArchiveCutoffMode))
                {
                    return BadRequest("Unknown archive cutoff mode: " + config.ArchiveCutoffMode);
                }
                if (config.ArchiveCutoffMode == ArchiveCutoffMode.CustomRollingDays && config.ArchiveCustomRollingDays is not (90 or 180 or 365))
                {
                    return BadRequest("Custom rolling-window cutoff must be one of the preset options (90, 180, or 365 days).");
                }
                if (config.ArchiveCutoffMode == ArchiveCutoffMode.CustomDate && config.ArchiveCustomCutoffDate is null)
                {
                    return BadRequest("A custom cutoff date is required for that mode.");
                }
                if (string.IsNullOrWhiteSpace(config.ArchiveHost) || string.IsNullOrWhiteSpace(config.ArchiveDatabaseName) || string.IsNullOrWhiteSpace(config.ArchiveUsername))
                {
                    return BadRequest("Archive DB host, database name, and username are required to enable database archiving.");
                }
                if (config.ArchivePort is < 1 or > 65535)
                {
                    return BadRequest("Archive DB port must be between 1 and 65535.");
                }
                // A stored password already existing means "Change archive database" is editing other fields without re-entering it - anything else means the admin is (re-)configuring archiving from scratch and must have already gone through TestArchiveDatabase with a real password.
                if (string.IsNullOrEmpty(config.ArchivePassword) && string.IsNullOrEmpty((await serverConfigRepo.ServerConfigGetAsync(1)).ArchivePassword))
                {
                    return BadRequest("Archive DB password is required - test the connection first.");
                }
            }

            config.IDServerConfig = 1; // single global row - the form never chooses this
            await serverConfigRepo.ServerConfigUpdateAsync(config);
            await WriteAuditAsync("ServerConfig.Updated", null, "ServerConfig", "1", null);
            return Ok();
        }

        /// Sends a real test message through the saved (not the unsaved form's) Email settings - "Save" first, then test - so an admin can confirm SMTP actually works without waiting on a real alert/activation email.
        [HttpPost("TestEmail")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> TestEmail(string toEmail)
        {
            if (!CallerIsGlobalAdmin)
            {
                return StatusCode(403, "Server-wide settings require the Global admin role");
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
                return StatusCode(403, "Server-wide settings require the Global admin role");
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

        /// Roadmap #209 - tests the UNSAVED form's archive DB credentials before ServerConfigApiController.Update ever persists them, so a bad host/port/password never silently disables archiving later. Password blank means "use whatever's already saved" (see ArchiveDbTestRequest's own remarks).
        [HttpPost("TestArchiveDatabase")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> TestArchiveDatabase([FromBody] ArchiveDbTestRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return StatusCode(403, "Server-wide settings require the Global admin role");
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

        /// Roadmap #209 - the "Data Archiving" subsection's own self-contained save (Web's ServerConfigController.SaveArchiveSettings JS button, not the main Server Settings form), independent of every other tab. Tests the connection first when enabling (skipped when Enabled is false - "disable" needs no working credentials, roadmap #209's own explicit design decision), only THEN persists.
        [HttpPost("ArchiveSettings")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public async Task<ActionResult> SaveArchiveSettings([FromBody] ArchiveSettingsSaveRequest request)
        {
            if (!CallerIsGlobalAdmin)
            {
                return StatusCode(403, "Server-wide settings require the Global admin role");
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

        /// Roadmap #419 - passive Server Health card, polled by the Web page on an interval (Agrumy.Web's ServerConfigController.Health -> live-refresh.js), not an on-demand test button like TestEmail/TestArchiveDatabase above. Only lists a dependency currently enabled/configured in ServerConfig - see ServerHealthService.
        [HttpGet("Health")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult<IReadOnlyList<ServerHealthEntry>>> GetHealth()
        {
            if (!CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader))
            {
                return StatusCode(403, "Server-wide settings require the Global admin role");
            }
            return Ok(await serverHealthService.GetStatusesAsync());
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
