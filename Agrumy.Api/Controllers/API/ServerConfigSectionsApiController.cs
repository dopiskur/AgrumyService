using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Utils;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// ServerConfig split by domain: each section has its own GET/PUT and a PUT rewrites only that section's fields on the single row, so no save can zero a field its form never rendered.
    [Route("api/ServerConfig")]
    public class ServerConfigSectionsApiController(IServerConfigRepository serverConfigRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        private const string GlobalOnly = "Server-wide settings require the Global admin role";

        [HttpGet("DeviceDefaults")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<DeviceDefaultsSettings>> GetDeviceDefaults() => GetSectionAsync(DeviceDefaultsSettings.From);

        [HttpPut("DeviceDefaults")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateDeviceDefaults([FromBody] DeviceDefaultsSettings s) => SaveSectionAsync(s, "DeviceDefaults", s =>
        {
            // Same bound DeviceApiController.DeviceConfigControllerUpdate enforces on a per-device override.
            if (!SafetyLimitValidation.IsValid(s.WaterPumpMaxRunSeconds))
            {
                return $"WaterPump max run time must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.";
            }
            if (!SafetyLimitValidation.IsValid(s.WaterPumpCooldownSeconds))
            {
                return $"WaterPump cooldown must be between 0 (disabled) and {SafetyLimitValidation.MaxReasonableSeconds} seconds.";
            }
            // Ceiling matches AgrumyFirmware DeviceModel.h's MAX_RULES - above it the firmware silently drops rules.
            if (s.MaxRulesPerZone is < 1 or > 32)
            {
                return "Max rules per zone must be between 1 and 32.";
            }
            if (s.ConfigHeartbeatHours is < 0 or > 168)
            {
                return "Config heartbeat must be 0 (disabled) or between 1 and 168 hours.";
            }
            return null;
        });

        [HttpGet("Accounts")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<AccountSettings>> GetAccounts() => GetSectionAsync(AccountSettings.From);

        [HttpPut("Accounts")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateAccounts([FromBody] AccountSettings s) => SaveSectionAsync(s, "Accounts", s =>
        {
            if (s.PasswordMinLength is < 4 or > 128)
            {
                return "Minimum password length must be between 4 and 128.";
            }
            if (s.ActivationResendCooldownMinutes is < 0)
            {
                return "Activation resend cooldown cannot be negative.";
            }
            // Fixed dropdown - a leaked PIN's blast radius is an admin-chosen preset, not free text.
            if (s.DevicePinValidMinutes is not (5 or 15 or 60 or 120 or 360 or 720 or 1440))
            {
                return "Registration PIN validity must be one of the preset options.";
            }
            return null;
        });

        [HttpGet("Alerts")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<AlertSettings>> GetAlerts() => GetSectionAsync(AlertSettings.From);

        [HttpPut("Alerts")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateAlerts([FromBody] AlertSettings s) => SaveSectionAsync(s, "Alerts", s =>
        {
            if (s.ProblemEventExpiryHours is not (1 or 6 or 12 or 24 or 48))
            {
                return "Problem alert expiry must be one of 1, 6, 12, 24, or 48 hours.";
            }
            if (s.EventDedupeMinutes is < 0)
            {
                return "Event dedupe minutes cannot be negative.";
            }
            return null;
        });

        [HttpGet("Firmware")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<FirmwareSettings>> GetFirmware() => GetSectionAsync(FirmwareSettings.From);

        [HttpPut("Firmware")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateFirmware([FromBody] FirmwareSettings s) => SaveSectionAsync(s, "Firmware", s =>
        {
            if (!Enum.IsDefined(s.FirmwareSource))
            {
                return "Unknown firmware source: " + s.FirmwareSource;
            }
            if (s.FirmwareSource == FirmwareSource.Custom &&
                (!Uri.TryCreate(s.FirmwareCustomRepositoryUrl, UriKind.Absolute, out Uri? customUri) || customUri.Scheme is not ("http" or "https")))
            {
                return "Custom firmware source needs an absolute http(s) manifest URL.";
            }
            // Blank = back to the appsettings seed (EfServerConfigRepository's ToDto fallback); a value must be owner/name.
            s.FirmwareGitHubRepository = string.IsNullOrWhiteSpace(s.FirmwareGitHubRepository) ? null : s.FirmwareGitHubRepository.Trim().Trim('/');
            if (s.FirmwareGitHubRepository != null && s.FirmwareGitHubRepository.Count(c => c == '/') != 1)
            {
                return "GitHub repository must be in owner/name form.";
            }
            if (s.FirmwareRefreshIntervalHours is < 0)
            {
                return "Firmware auto-refresh interval must be 0/empty (disabled) or a positive number of hours.";
            }
            return null;
        });

        [HttpGet("DataRetention")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<DataRetentionSettings>> GetDataRetention() => GetSectionAsync(DataRetentionSettings.From);

        [HttpPut("DataRetention")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateDataRetention([FromBody] DataRetentionSettings s) => SaveSectionAsync(s, "DataRetention", s =>
        {
            if (s.SensorDataRetentionDays is < 0)
            {
                return "Sensor data retention days must be 0/empty (disabled) or a positive number.";
            }
            if (s.RecycleBinRetentionDays is < 0 or > 90)
            {
                return "Recycle bin retention must be between 0 and 90 days.";
            }
            if (s.SatelliteRasterRetentionDays is < 0 or > 365)
            {
                return "Satellite raster cache retention must be between 0 and 365 days.";
            }
            return null;
        });

        [HttpGet("Weather")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<WeatherSettings>> GetWeather() => GetSectionAsync(WeatherSettings.From);

        [HttpPut("Weather")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateWeather([FromBody] WeatherSettings s) => SaveSectionAsync(s, "Weather", s =>
        {
            // One coordinate without the other would silently query (lat, 0) or (0, lon).
            if (s.WeatherLocationLat.HasValue != s.WeatherLocationLon.HasValue)
            {
                return "Weather latitude and longitude must both be set, or both left empty.";
            }
            if (s.WeatherLocationLat is < -90 or > 90)
            {
                return "Weather latitude must be between -90 and 90.";
            }
            if (s.WeatherLocationLon is < -180 or > 180)
            {
                return "Weather longitude must be between -180 and 180.";
            }
            if (s.WeatherPollIntervalMinutes is < 1)
            {
                return "Weather poll interval must be at least 1 minute.";
            }
            if (s.WeatherRainSkipThreshold is < 0 or > 100)
            {
                return "Weather rain-skip threshold must be between 0 and 100 percent.";
            }
            if (s.FrostLookaheadHours is < 1 or > 48)
            {
                return "Frost lookahead must be between 1 and 48 hours.";
            }
            if (s.FrostCloudinessMaxPercent is < 0 or > 100)
            {
                return "Frost max cloudiness must be between 0 and 100 percent.";
            }
            if (s.FrostWindMaxMetersPerSecond is < 0)
            {
                return "Frost max wind speed cannot be negative.";
            }
            return null;
        });

        [HttpGet("Gateway")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<GatewaySettings>> GetGateway() => GetSectionAsync(GatewaySettings.From);

        [HttpPut("Gateway")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateGateway([FromBody] GatewaySettings s) => SaveSectionAsync(s, "Gateway", s =>
        {
            if (s.GatewayWaitWindowSeconds is < 10 or > 300)
            {
                return "Gateway wait window must be between 10 and 300 seconds.";
            }
            if (!Enum.IsDefined(s.GatewayMode))
            {
                return "Unknown gateway mode: " + s.GatewayMode;
            }
            return null;
        });

        [HttpGet("Mqtt")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<MqttSettings>> GetMqtt() => GetSectionAsync(MqttSettings.From);

        [HttpPut("Mqtt")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateMqtt([FromBody] MqttSettings s) => SaveSectionAsync(s, "Mqtt", s =>
        {
            if (s.MqttBrokerPort is < 1 or > 65535)
            {
                return "MQTT broker port must be between 1 and 65535.";
            }
            if (s.MqttTransportEnabled && string.IsNullOrWhiteSpace(s.MqttBrokerHost))
            {
                return "MQTT broker host is required to enable MQTT instant command push.";
            }
            return null;
        });

        [HttpGet("Email")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<EmailSettings>> GetEmail() => GetSectionAsync(EmailSettings.From);

        [HttpPut("Email")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateEmail([FromBody] EmailSettings s) => SaveSectionAsync(s, "Email", s =>
        {
            if (s.EmailPort is < 1 or > 65535)
            {
                return "SMTP port must be between 1 and 65535.";
            }
            if (s.EmailEnabled && (string.IsNullOrWhiteSpace(s.EmailHost) || string.IsNullOrWhiteSpace(s.EmailFromAddress)))
            {
                return "SMTP host and From address are required to enable email notifications.";
            }
            return null;
        });

        [HttpGet("Webhook")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<WebhookSettings>> GetWebhook() => GetSectionAsync(WebhookSettings.From);

        [HttpPut("Webhook")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateWebhook([FromBody] WebhookSettings s) => SaveSectionAsync(s, "Webhook", s =>
            s.WebhookEnabled && !Uri.TryCreate(s.WebhookUrl, UriKind.Absolute, out _)
                ? "An absolute webhook URL is required to enable webhook notifications."
                : null);

        [HttpGet("OData")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<ODataSettings>> GetOData() => GetSectionAsync(ODataSettings.From);

        [HttpPut("OData")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateOData([FromBody] ODataSettings s) => SaveSectionAsync(s, "OData", _ => null);

        [HttpGet("Arkod")]
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public Task<ActionResult<ArkodSettings>> GetArkod() => GetSectionAsync(ArkodSettings.From);

        [HttpPut("Arkod")]
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        public Task<ActionResult> UpdateArkod([FromBody] ArkodSettings s) => SaveSectionAsync(s, "Arkod", _ => null);

        private async Task<ActionResult<T>> GetSectionAsync<T>(Func<ServerConfig, T> from)
        {
            if (!CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader))
            {
                return ForbidWith(GlobalOnly);
            }
            return Ok(from(await serverConfigRepo.ServerConfigGetAsync(1)));
        }

        private async Task<ActionResult> SaveSectionAsync<T>(T section, string sectionName, Func<T, string?> validate) where T : IServerConfigSection
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith(GlobalOnly);
            }
            string? error = validate(section);
            if (error != null)
            {
                return BadRequest(error);
            }
            ServerConfig current = await serverConfigRepo.ServerConfigGetAsync(1);
            // The repo hands back decrypted secrets and treats blank as "keep stored", so only the owning section may resend one.
            current.MqttPassword = null;
            current.EmailPassword = null;
            current.WebhookSecret = null;
            current.ArchivePassword = null;
            section.ApplyTo(current);
            current.IDServerConfig = 1;
            await serverConfigRepo.ServerConfigUpdateAsync(current);
            await WriteAuditAsync($"ServerConfig.{sectionName}Updated", null, "ServerConfig", "1", null);
            return Ok();
        }
    }
}
