using System.ComponentModel.DataAnnotations;

namespace Agrumy.Shared.Models
{
    public class ServerConfig
    {
        public int? IDServerConfig { get; set; }
        public int? PortHTTP { get; set; }
        public int? PortHTTPS { get; set; }

        // Server-wide defaults seeded from ServerConfig:Hysteresis, then runtime-editable; copied onto a new device's DeviceConfigController row, which is independently editable afterward under Device -> Controller.
        public double? WaterLevelHysteresis { get; set; }
        public double? TemperatureHysteresis { get; set; }
        public double? HumidityHysteresis { get; set; }
        public double? LightHysteresis { get; set; }

        // LowBatteryAlertEvaluator's threshold/hysteresis (percent of sensorData.Battery), no per-device override; fires at Battery<=Threshold, clears at Battery>=Threshold+Hysteresis.
        public double? BatteryLowThreshold { get; set; }
        public double? BatteryLowHysteresis { get; set; }

        // TankRefillAlertEvaluator's threshold/hysteresis (percent of TankCalculator fill), global like Battery's - a physical tank's own capacity/calibration is per-zone (DeviceFarmUnitZone), but "how empty is too empty" is one policy for the whole tenant.
        public double? TankRefillThreshold { get; set; }
        public double? TankRefillHysteresis { get; set; }

        // WaterPump-only hard safety limits (seconds, null/0 disables); enforced device-side by ActuatorController::applyWaterPumpSafetyLimits regardless of control mode, overridable per-device via DeviceConfigController.
        public int? WaterPumpMaxRunSeconds { get; set; }
        public int? WaterPumpCooldownSeconds { get; set; }

        // A device repeating the identical DeviceEventType within this many minutes is ignored server-side rather than stored.
        public int? EventDedupeMinutes { get; set; }

        // Gates whether non-critical problem events (crash/auth/sync/OTA) turn a Unit/Zone Orange at all - see Agrumy.Api.Dal.EfRepository.ComputeStatus.
        [Display(Name = "Alert on non-critical device problems")]
        public bool ProblemEventAlertsEnabled { get; set; } = true;

        // How long an un-acknowledged problem event keeps a Unit/Zone Orange, clamped to {1,6,12,24,48} by ServerConfigApiController.Update; acknowledging clears it immediately regardless.
        [Display(Name = "Problem alert expiry (hours)")]
        public int ProblemEventExpiryHours { get; set; } = 24;

        // Minimum minutes between "resend activation email" requests for the same user - default 10.
        public int? ActivationResendCooldownMinutes { get; set; }

        // Default 10, hard ceiling 32 (AgrumyFirmware DeviceModel.h's MAX_RULES) - see ServerConfigApiController.Update for the enforced bound.
        public int? MaxRulesPerZone { get; set; }

        // Allows UserRegistration to create an unrecognized tenant name instead of rejecting; non-nullable because bool? would render asp-for as a text box, not a checkbox.
        [Display(Name = "Allow self-service tenant creation")]
        public bool AllowSelfServiceTenantCreation { get; set; }

        // Gates the Tenant Management menu item alongside the GlobalAdmin role check (_Layout.cshtml), so a fresh install doesn't expose cross-tenant management by default.
        [Display(Name = "Enable Tenant Management page")]
        public bool TenantManagementEnabled { get; set; }

        // Where firmware comes from (Agrumy.Shared.Models.FirmwareSource); GitHub defaults for zero-setup installs. String on the wire (Refit enum-as-name) since this admin DTO doesn't touch the device-facing raw-int convention.
        [Display(Name = "Firmware source")]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public FirmwareSource FirmwareSource { get; set; }

        [Display(Name = "GitHub repository (owner/name)")]
        public string? FirmwareGitHubRepository { get; set; }

        /// Custom mode only: absolute URL of a manifest.json in Agrumy.Shared.Models.FirmwareManifest's format - its .bin URLs may be absolute or relative to the manifest's own location.
        [Display(Name = "Custom repository manifest URL")]
        public string? FirmwareCustomRepositoryUrl { get; set; }

        // Null/0 disables auto-refresh; FirmwareCatalogRefreshBackgroundService re-reads this live value every minute, same pattern as WeatherPollIntervalMinutes.
        [Display(Name = "Auto-refresh catalog every (hours)")]
        public int? FirmwareRefreshIntervalHours { get; set; }

        // Written only by FirmwareCatalogRefreshEvaluator (ServerConfigFirmwareRefreshStateSetAsync), same reasoning as WeatherCheckedAtUtc, so a stale admin form post can't clobber it.
        [Display(Name = "Catalog last auto-refreshed")]
        public DateTimeOffset? FirmwareLastRefreshedAtUtc { get; set; }

        // Days of sensorData history to auto-purge - Postgres/TimescaleDB via add_retention_policy, MariaDB/MySQL via SensorDataRetentionBackgroundService's daily purge; null/0 disables it.
        [Display(Name = "Sensor data retention (days)")]
        public int? SensorDataRetentionDays { get; set; }

        // Roadmap #409 - how long a soft-deleted Farm/Device stays listed (and restorable) in the Recycle Bin; 0-90, default 30. Past this, a device/farm just drops off the recycle bin listing - its row (and SensorData) is NOT auto-purged, that's the separate manual/schedulable "Purge orphaned sensor data" action below.
        [Display(Name = "Recycle bin retention (days)")]
        [Range(0, 90)]
        public int? RecycleBinRetentionDays { get; set; }

        // PurgeOrphanedSensorDataBackgroundService only runs daily when this is on - the manual "Purge orphaned sensor data" trigger in the Database section works regardless.
        [Display(Name = "Schedule daily orphaned sensor data purge")]
        public bool PurgeOrphanedSensorDataScheduleEnabled { get; set; }

        // Install-wide location OpenWeatherMap forecasts are pulled for; null leaves WeatherBackgroundService inert rather than failing loudly.
        [Display(Name = "Latitude")]
        public double? WeatherLocationLat { get; set; }
        [Display(Name = "Longitude")]
        public double? WeatherLocationLon { get; set; }

        // Admin-editable poll cadence - WeatherBackgroundService ticks every minute but only calls the API once this many minutes have elapsed since WeatherCheckedAtUtc, making the interval live-editable without a restart.
        [Display(Name = "Forecast poll interval (minutes)")]
        public int? WeatherPollIntervalMinutes { get; set; }

        // Rain-probability percentage (OpenWeatherMap's "pop" field) at or above which WeatherEvaluator sets WeatherRainPredicted.
        [Display(Name = "Rain-skip threshold (%)")]
        public double? WeatherRainSkipThreshold { get; set; }

        // WeatherEvaluator's last result - read-only on Server Settings, written only through ServerConfigWeatherStateSetAsync so a stale admin form post can't clobber a fresher reading.
        [Display(Name = "Rain predicted")]
        public bool WeatherRainPredicted { get; set; }
        [Display(Name = "Forecast last checked")]
        public DateTimeOffset? WeatherCheckedAtUtc { get; set; }

        // Gates the Gateway Devices admin page (_Layout.cshtml, same pattern as TenantManagementEnabled) and whether GatewayApiController accepts Batch calls at all.
        [Display(Name = "Enable Agrumy.Gateway support")]
        public bool GatewayEnabled { get; set; }

        [Display(Name = "Gateway mode")]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public GatewayMode GatewayMode { get; set; }

        // Aggregated mode only (see GatewayBatchResponse); clamped 10-300 by ServerConfigApiController.Update, same pattern as MaxRulesPerZone.
        [Display(Name = "Aggregated wait window (seconds)")]
        public int GatewayWaitWindowSeconds { get; set; } = 30;

        // Enforced by Agrumy.Shared.Security.PasswordPolicy wherever a NEW password is set; clamped 4-128 by ServerConfigApiController.Update.
        [Display(Name = "Minimum password length")]
        public int PasswordMinLength { get; set; } = 8;

        [Display(Name = "Require upper/lower case, digit, and symbol")]
        public bool PasswordRequireComplexity { get; set; }

        // 0 disables it. DeviceConfigBuilder recomputes UtcOffsetSeconds/SkipWaterPumpForRain fresh on every build, but neither bumps ConfigVersion when it changes (a DST transition, an admin edit to ScheduleTimeZone, or a weather-poll flip) - this forces a full config resend periodically so those changes still reach a device that otherwise has nothing else queued. Clamped 1-168 (a week) by ServerConfigApiController.Update when non-zero. Default 1h, not 24h (roadmap #396(2)) - DST/rain-veto changes were tolerating up to a full day of staleness before this.
        [Display(Name = "Config heartbeat (hours, 0 = off)")]
        public int ConfigHeartbeatHours { get; set; } = 1;

        // Opt-in alternative alongside the HTTP/JWT poll cycle: when enabled, a newly-queued command is also published immediately to this broker so a persistently-connected device (AgrumyFirmware's MqttController) can act before its next HTTP poll, instead of only through CommandQueueService/GetPendingCommandAsync; OTA/registration/firmware distribution stay on HTTP (see Agrumy.Api.Commands.MqttCommandPublisher).
        [Display(Name = "Enable MQTT instant command push")]
        public bool MqttTransportEnabled { get; set; }

        [Display(Name = "Broker host")]
        public string? MqttBrokerHost { get; set; }

        [Display(Name = "Broker port")]
        public int MqttBrokerPort { get; set; } = 1883;

        [Display(Name = "Broker username")]
        public string? MqttUsername { get; set; }

        // Never round-tripped back into the edit form - see ServerConfigController (Web)'s "blank keeps the existing value" handling.
        [Display(Name = "Broker password")]
        public string? MqttPassword { get; set; }

        // SMTP email delivery config, DB-backed replacement for the old appsettings-only Notifications:Email section - see Agrumy.Api.Notifications.EmailNotificationChannel, which now reads this instead.
        [Display(Name = "Enable email notifications")]
        public bool EmailEnabled { get; set; }

        [Display(Name = "SMTP host")]
        public string? EmailHost { get; set; }

        [Display(Name = "SMTP port")]
        public int EmailPort { get; set; } = 587;

        /// 587 with STARTTLS (true, default) vs 465 implicit TLS (false).
        [Display(Name = "Use STARTTLS")]
        public bool EmailUseStartTls { get; set; } = true;

        [Display(Name = "SMTP username")]
        public string? EmailUsername { get; set; }

        // Never round-tripped back into the edit form - same "blank keeps existing" handling as MqttPassword above.
        [Display(Name = "SMTP password")]
        public string? EmailPassword { get; set; }

        [Display(Name = "From address")]
        public string? EmailFromAddress { get; set; }

        [Display(Name = "From name")]
        public string EmailFromName { get; set; } = "Agrumy";

        // A leaked/screen-shotted PIN (or a leaked DeviceCommand.Payload, see DeviceCommandApiController.GetCommand) is valid for this long - fixed preset {5,15,60,120,360,720,1440}, clamped by ServerConfigApiController.Update, not free-text.
        [Display(Name = "Registration PIN validity (minutes)")]
        public int DevicePinValidMinutes { get; set; } = 60;

        // Roadmap #209 - MariaDB/MySQL only (Postgres/TimescaleDB uses its own native tiered storage instead, #14); opt-in, moves sensorData rows past the cutoff to a separate archive database instead of deleting them (SensorDataArchiveEvaluator). Inactive until an admin sets and successfully tests archive credentials.
        [Display(Name = "Enable database archiving")]
        public bool ArchiveEnabled { get; set; }

        [Display(Name = "Archive cutoff mode")]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public ArchiveCutoffMode ArchiveCutoffMode { get; set; }

        /// CustomDate mode only - rows with DateCreated before this (UTC midnight) are archived.
        [Display(Name = "Custom cutoff date")]
        public DateOnly? ArchiveCustomCutoffDate { get; set; }

        /// CustomRollingDays mode only - fixed preset {90,180,365}, clamped by ServerConfigApiController.Update.
        [Display(Name = "Archive data older than (days)")]
        public int? ArchiveCustomRollingDays { get; set; }

        [Display(Name = "Archive DB host")]
        public string? ArchiveHost { get; set; }
        [Display(Name = "Archive DB port")]
        public int? ArchivePort { get; set; } = 3306;
        [Display(Name = "Archive database name")]
        public string? ArchiveDatabaseName { get; set; }
        [Display(Name = "Archive DB username")]
        public string? ArchiveUsername { get; set; }
        // Never round-tripped back into the edit form - same "blank keeps existing" convention as MqttPassword/EmailPassword above.
        [Display(Name = "Archive DB password")]
        public string? ArchivePassword { get; set; }

        // Written only by SensorDataArchiveEvaluator, read-only on Server Settings - same isolation reasoning as WeatherCheckedAtUtc/FirmwareLastRefreshedAtUtc.
        [Display(Name = "Archive last ran")]
        public DateTimeOffset? ArchiveLastRunAtUtc { get; set; }
    }

    /// ServerConfig.ArchiveCutoffMode - which rows SensorDataArchiveEvaluator moves out of the active database on its next run.
    public enum ArchiveCutoffMode
    {
        /// Yearly: on/after Jan 1, everything from two calendar years back (and older) is archived - e.g. on 2027-01-01, 2025 and earlier moves, 2026+ stays active.
        Calendar = 0,
        /// Everything strictly before ArchiveCustomCutoffDate moves, a one-time cutoff rather than a moving window.
        CustomDate = 1,
        /// Everything older than ArchiveCustomRollingDays moves, re-evaluated (and so effectively continuous) on every run.
        CustomRollingDays = 2,
    }

    /// The only ServerConfig field a pre-login, unauthenticated page may see - Register uses it to decide whether to show "create a new tenant" without needing the admin-only /api/ServerConfig.
    public class PublicServerConfig
    {
        public bool AllowSelfServiceTenantCreation { get; set; }
    }

    /// Body of POST /api/ServerConfig/TestArchiveDatabase (roadmap #209) - tests connectivity BEFORE Update ever saves these as the real archive credentials. Password blank means "use whatever's already saved" (the "Change archive database" flow editing host/port/etc without re-entering an unchanged password), same convention ServerConfigApiController.Update itself uses.
    public class ArchiveDbTestRequest
    {
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? DatabaseName { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    /// One row of GET /api/ServerConfig/Health (roadmap #419) - Status mirrors Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.ToString() ("Healthy"/"Degraded"/"Unhealthy"), kept as a plain string here so Agrumy.Web/Agrumy.Shared don't need a reference to that package just to deserialize it.
    public sealed record ServerHealthEntry(string Name, string Status, string? Description, double DurationMs);

    /// Body of POST /api/ServerConfig/ArchiveSettings (roadmap #209) - the "Data Archiving" subsection's own self-contained save, independent of the main Server Settings form/button: tests the connection first when Enabled (skipped when disabling - see ServerConfigApiController.SaveArchiveSettings), then persists only these archive-specific fields, leaving the rest of ServerConfig untouched.
    public class ArchiveSettingsSaveRequest
    {
        public bool Enabled { get; set; }
        public ArchiveCutoffMode CutoffMode { get; set; }
        public DateOnly? CustomCutoffDate { get; set; }
        public int? CustomRollingDays { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? DatabaseName { get; set; }
        public string? Username { get; set; }
        // Blank means "keep whatever's already saved" - same convention as ArchiveDbTestRequest.Password.
        public string? Password { get; set; }
    }
}
