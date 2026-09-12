using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Agrumy.Shared.Models
{
    /// One domain slice of ServerConfig with its own GET/PUT - a section save can only touch the fields it declares, so a form that never rendered another section's field can't clobber it.
    public interface IServerConfigSection
    {
        void ApplyTo(ServerConfig config);
    }

    /// Values a newly-added device's Controller config is seeded with, plus the config-resend heartbeat.
    public sealed class DeviceDefaultsSettings : IServerConfigSection
    {
        [Display(Name = "Config heartbeat (hours, 0 = off)")]
        public int ConfigHeartbeatHours { get; set; } = 1;
        public double? WaterLevelHysteresis { get; set; }
        public double? TemperatureHysteresis { get; set; }
        public double? HumidityHysteresis { get; set; }
        public double? LightHysteresis { get; set; }
        public int? MaxRulesPerZone { get; set; }
        public int? WaterPumpMaxRunSeconds { get; set; }
        public int? WaterPumpCooldownSeconds { get; set; }

        public static DeviceDefaultsSettings From(ServerConfig c) => new()
        {
            ConfigHeartbeatHours = c.ConfigHeartbeatHours,
            WaterLevelHysteresis = c.WaterLevelHysteresis,
            TemperatureHysteresis = c.TemperatureHysteresis,
            HumidityHysteresis = c.HumidityHysteresis,
            LightHysteresis = c.LightHysteresis,
            MaxRulesPerZone = c.MaxRulesPerZone,
            WaterPumpMaxRunSeconds = c.WaterPumpMaxRunSeconds,
            WaterPumpCooldownSeconds = c.WaterPumpCooldownSeconds,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.ConfigHeartbeatHours = ConfigHeartbeatHours;
            c.WaterLevelHysteresis = WaterLevelHysteresis;
            c.TemperatureHysteresis = TemperatureHysteresis;
            c.HumidityHysteresis = HumidityHysteresis;
            c.LightHysteresis = LightHysteresis;
            c.MaxRulesPerZone = MaxRulesPerZone;
            c.WaterPumpMaxRunSeconds = WaterPumpMaxRunSeconds;
            c.WaterPumpCooldownSeconds = WaterPumpCooldownSeconds;
        }
    }

    public sealed class AccountSettings : IServerConfigSection
    {
        public int? ActivationResendCooldownMinutes { get; set; }
        [Display(Name = "Minimum password length")]
        public int PasswordMinLength { get; set; } = 8;
        [Display(Name = "Require upper/lower case, digit, and symbol")]
        public bool PasswordRequireComplexity { get; set; }
        [Display(Name = "Registration PIN validity (minutes)")]
        public int DevicePinValidMinutes { get; set; } = 60;

        public static AccountSettings From(ServerConfig c) => new()
        {
            ActivationResendCooldownMinutes = c.ActivationResendCooldownMinutes,
            PasswordMinLength = c.PasswordMinLength,
            PasswordRequireComplexity = c.PasswordRequireComplexity,
            DevicePinValidMinutes = c.DevicePinValidMinutes,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.ActivationResendCooldownMinutes = ActivationResendCooldownMinutes;
            c.PasswordMinLength = PasswordMinLength;
            c.PasswordRequireComplexity = PasswordRequireComplexity;
            c.DevicePinValidMinutes = DevicePinValidMinutes;
        }
    }

    /// Server-wide alert defaults; an organization's own TenantAlertConfig overrides these per organization.
    public sealed class AlertSettings : IServerConfigSection
    {
        public double? BatteryLowThreshold { get; set; }
        public double? BatteryLowHysteresis { get; set; }
        public double? TankRefillThreshold { get; set; }
        public double? TankRefillHysteresis { get; set; }
        public int? EventDedupeMinutes { get; set; }
        [Display(Name = "Alert on non-critical device problems")]
        public bool ProblemEventAlertsEnabled { get; set; } = true;
        [Display(Name = "Problem alert expiry (hours)")]
        public int ProblemEventExpiryHours { get; set; } = 24;

        public static AlertSettings From(ServerConfig c) => new()
        {
            BatteryLowThreshold = c.BatteryLowThreshold,
            BatteryLowHysteresis = c.BatteryLowHysteresis,
            TankRefillThreshold = c.TankRefillThreshold,
            TankRefillHysteresis = c.TankRefillHysteresis,
            EventDedupeMinutes = c.EventDedupeMinutes,
            ProblemEventAlertsEnabled = c.ProblemEventAlertsEnabled,
            ProblemEventExpiryHours = c.ProblemEventExpiryHours,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.BatteryLowThreshold = BatteryLowThreshold;
            c.BatteryLowHysteresis = BatteryLowHysteresis;
            c.TankRefillThreshold = TankRefillThreshold;
            c.TankRefillHysteresis = TankRefillHysteresis;
            c.EventDedupeMinutes = EventDedupeMinutes;
            c.ProblemEventAlertsEnabled = ProblemEventAlertsEnabled;
            c.ProblemEventExpiryHours = ProblemEventExpiryHours;
        }
    }

    public sealed class FirmwareSettings : IServerConfigSection
    {
        [Display(Name = "Firmware source")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FirmwareSource FirmwareSource { get; set; }
        [Display(Name = "GitHub repository (owner/name)")]
        public string? FirmwareGitHubRepository { get; set; }
        [Display(Name = "Custom repository manifest URL")]
        public string? FirmwareCustomRepositoryUrl { get; set; }
        [Display(Name = "Auto-refresh catalog every (hours)")]
        public int? FirmwareRefreshIntervalHours { get; set; }
        /// Read-only here - FirmwareCatalogRefreshEvaluator is its only writer.
        public DateTimeOffset? FirmwareLastRefreshedAtUtc { get; set; }

        public static FirmwareSettings From(ServerConfig c) => new()
        {
            FirmwareSource = c.FirmwareSource,
            FirmwareGitHubRepository = c.FirmwareGitHubRepository,
            FirmwareCustomRepositoryUrl = c.FirmwareCustomRepositoryUrl,
            FirmwareRefreshIntervalHours = c.FirmwareRefreshIntervalHours,
            FirmwareLastRefreshedAtUtc = c.FirmwareLastRefreshedAtUtc,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.FirmwareSource = FirmwareSource;
            c.FirmwareGitHubRepository = FirmwareGitHubRepository;
            c.FirmwareCustomRepositoryUrl = FirmwareCustomRepositoryUrl;
            c.FirmwareRefreshIntervalHours = FirmwareRefreshIntervalHours;
        }
    }

    public sealed class DataRetentionSettings : IServerConfigSection
    {
        [Display(Name = "Sensor data retention (days)")]
        public int? SensorDataRetentionDays { get; set; }
        [Display(Name = "Satellite raster cache retention (days)")]
        [Range(0, 365)]
        public int? SatelliteRasterRetentionDays { get; set; } = 30;
        [Display(Name = "Recycle bin retention (days)")]
        [Range(0, 90)]
        public int? RecycleBinRetentionDays { get; set; }
        [Display(Name = "Schedule daily orphaned sensor data purge")]
        public bool PurgeOrphanedSensorDataScheduleEnabled { get; set; }

        public static DataRetentionSettings From(ServerConfig c) => new()
        {
            SensorDataRetentionDays = c.SensorDataRetentionDays,
            SatelliteRasterRetentionDays = c.SatelliteRasterRetentionDays,
            RecycleBinRetentionDays = c.RecycleBinRetentionDays,
            PurgeOrphanedSensorDataScheduleEnabled = c.PurgeOrphanedSensorDataScheduleEnabled,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.SensorDataRetentionDays = SensorDataRetentionDays;
            c.SatelliteRasterRetentionDays = SatelliteRasterRetentionDays;
            c.RecycleBinRetentionDays = RecycleBinRetentionDays;
            c.PurgeOrphanedSensorDataScheduleEnabled = PurgeOrphanedSensorDataScheduleEnabled;
        }
    }

    public sealed class WeatherSettings : IServerConfigSection
    {
        [Display(Name = "Default latitude")]
        public double? WeatherLocationLat { get; set; }
        [Display(Name = "Default longitude")]
        public double? WeatherLocationLon { get; set; }
        [Display(Name = "Forecast poll interval (minutes)")]
        public int? WeatherPollIntervalMinutes { get; set; }
        [Display(Name = "Rain-skip threshold (%)")]
        public double? WeatherRainSkipThreshold { get; set; }
        [Display(Name = "Frost lookahead (hours)")]
        public int? FrostLookaheadHours { get; set; }
        [Display(Name = "Frost temperature threshold (°C)")]
        public double? FrostTempThresholdC { get; set; }
        [Display(Name = "Frost max cloudiness (%)")]
        public double? FrostCloudinessMaxPercent { get; set; }
        [Display(Name = "Frost max wind speed (m/s)")]
        public double? FrostWindMaxMetersPerSecond { get; set; }

        public static WeatherSettings From(ServerConfig c) => new()
        {
            WeatherLocationLat = c.WeatherLocationLat,
            WeatherLocationLon = c.WeatherLocationLon,
            WeatherPollIntervalMinutes = c.WeatherPollIntervalMinutes,
            WeatherRainSkipThreshold = c.WeatherRainSkipThreshold,
            FrostLookaheadHours = c.FrostLookaheadHours,
            FrostTempThresholdC = c.FrostTempThresholdC,
            FrostCloudinessMaxPercent = c.FrostCloudinessMaxPercent,
            FrostWindMaxMetersPerSecond = c.FrostWindMaxMetersPerSecond,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.WeatherLocationLat = WeatherLocationLat;
            c.WeatherLocationLon = WeatherLocationLon;
            c.WeatherPollIntervalMinutes = WeatherPollIntervalMinutes;
            c.WeatherRainSkipThreshold = WeatherRainSkipThreshold;
            c.FrostLookaheadHours = FrostLookaheadHours;
            c.FrostTempThresholdC = FrostTempThresholdC;
            c.FrostCloudinessMaxPercent = FrostCloudinessMaxPercent;
            c.FrostWindMaxMetersPerSecond = FrostWindMaxMetersPerSecond;
        }
    }

    public sealed class GatewaySettings : IServerConfigSection
    {
        [Display(Name = "Enable Agrumy.Gateway support")]
        public bool GatewayEnabled { get; set; }
        [Display(Name = "Gateway mode")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public GatewayMode GatewayMode { get; set; }
        [Display(Name = "Aggregated wait window (seconds)")]
        public int GatewayWaitWindowSeconds { get; set; } = 30;

        public static GatewaySettings From(ServerConfig c) => new()
        {
            GatewayEnabled = c.GatewayEnabled,
            GatewayMode = c.GatewayMode,
            GatewayWaitWindowSeconds = c.GatewayWaitWindowSeconds,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.GatewayEnabled = GatewayEnabled;
            c.GatewayMode = GatewayMode;
            c.GatewayWaitWindowSeconds = GatewayWaitWindowSeconds;
        }
    }

    public sealed class MqttSettings : IServerConfigSection
    {
        [Display(Name = "Enable MQTT instant command push")]
        public bool MqttTransportEnabled { get; set; }
        [Display(Name = "Broker host")]
        public string? MqttBrokerHost { get; set; }
        [Display(Name = "Broker port")]
        public int MqttBrokerPort { get; set; } = 1883;
        [Display(Name = "Broker username")]
        public string? MqttUsername { get; set; }
        /// Never returned by GET; blank on PUT keeps the stored value.
        [Display(Name = "Broker password")]
        public string? MqttPassword { get; set; }
        public DateTimeOffset? MqttCredentialsSyncedAtUtc { get; set; }

        public static MqttSettings From(ServerConfig c) => new()
        {
            MqttTransportEnabled = c.MqttTransportEnabled,
            MqttBrokerHost = c.MqttBrokerHost,
            MqttBrokerPort = c.MqttBrokerPort,
            MqttUsername = c.MqttUsername,
            MqttCredentialsSyncedAtUtc = c.MqttCredentialsSyncedAtUtc,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.MqttTransportEnabled = MqttTransportEnabled;
            c.MqttBrokerHost = MqttBrokerHost;
            c.MqttBrokerPort = MqttBrokerPort;
            c.MqttUsername = MqttUsername;
            c.MqttPassword = MqttPassword;
        }
    }

    public sealed class EmailSettings : IServerConfigSection
    {
        [Display(Name = "Enable email notifications")]
        public bool EmailEnabled { get; set; }
        [Display(Name = "SMTP host")]
        public string? EmailHost { get; set; }
        [Display(Name = "SMTP port")]
        public int EmailPort { get; set; } = 587;
        [Display(Name = "Use STARTTLS")]
        public bool EmailUseStartTls { get; set; } = true;
        [Display(Name = "SMTP username")]
        public string? EmailUsername { get; set; }
        /// Never returned by GET; blank on PUT keeps the stored value.
        [Display(Name = "SMTP password")]
        public string? EmailPassword { get; set; }
        [Display(Name = "From address")]
        public string? EmailFromAddress { get; set; }
        [Display(Name = "From name")]
        public string EmailFromName { get; set; } = "Agrumy";

        public static EmailSettings From(ServerConfig c) => new()
        {
            EmailEnabled = c.EmailEnabled,
            EmailHost = c.EmailHost,
            EmailPort = c.EmailPort,
            EmailUseStartTls = c.EmailUseStartTls,
            EmailUsername = c.EmailUsername,
            EmailFromAddress = c.EmailFromAddress,
            EmailFromName = c.EmailFromName,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.EmailEnabled = EmailEnabled;
            c.EmailHost = EmailHost;
            c.EmailPort = EmailPort;
            c.EmailUseStartTls = EmailUseStartTls;
            c.EmailUsername = EmailUsername;
            c.EmailPassword = EmailPassword;
            c.EmailFromAddress = EmailFromAddress;
            c.EmailFromName = EmailFromName;
        }
    }

    public sealed class WebhookSettings : IServerConfigSection
    {
        [Display(Name = "Enable webhook notifications")]
        public bool WebhookEnabled { get; set; }
        [Display(Name = "Webhook URL")]
        public string? WebhookUrl { get; set; }
        /// Never returned by GET; blank on PUT keeps the stored value.
        [Display(Name = "Webhook signing secret")]
        public string? WebhookSecret { get; set; }

        public static WebhookSettings From(ServerConfig c) => new()
        {
            WebhookEnabled = c.WebhookEnabled,
            WebhookUrl = c.WebhookUrl,
        };

        public void ApplyTo(ServerConfig c)
        {
            c.WebhookEnabled = WebhookEnabled;
            c.WebhookUrl = WebhookUrl;
            c.WebhookSecret = WebhookSecret;
        }
    }

    public sealed class ODataSettings : IServerConfigSection
    {
        [Display(Name = "Enable Power BI / OData feed")]
        public bool ODataEnabled { get; set; }

        public static ODataSettings From(ServerConfig c) => new() { ODataEnabled = c.ODataEnabled };

        public void ApplyTo(ServerConfig c) => c.ODataEnabled = ODataEnabled;
    }

    public sealed class ArkodSettings : IServerConfigSection
    {
        [Display(Name = "Enable ARKOD GeoPackage sync")]
        public bool ArkodGeoPackageSyncEnabled { get; set; }
        /// Read-only here - ArkodGeoPackageSyncService is its only writer.
        public DateTimeOffset? ArkodGeoPackageSyncedAtUtc { get; set; }

        public static ArkodSettings From(ServerConfig c) => new()
        {
            ArkodGeoPackageSyncEnabled = c.ArkodGeoPackageSyncEnabled,
            ArkodGeoPackageSyncedAtUtc = c.ArkodGeoPackageSyncedAtUtc,
        };

        public void ApplyTo(ServerConfig c) => c.ArkodGeoPackageSyncEnabled = ArkodGeoPackageSyncEnabled;
    }
}
