using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Agrumy.Shared
{
    /// Populated via Bind() from the host's own IConfiguration (env-var/user-secrets overrides included), not a static singleton, so it stays constructor-injectable and mockable.
    public class AgrumySettings
    {
        public string? DefaultConnection { get; set; }

        /// Raw Database:Provider value (mysql|mariadb|postgres|postgresql); AGRUMY_DB_PROVIDER env var takes precedence, null/empty defaults to mysql via Agrumy.Dal.DbProviderKindParser.
        public string? DatabaseProvider { get; set; }

        public string? JwtSecureKey { get; set; }
        public string? JwtIssuer { get; set; }
        public string? JwtAudience { get; set; }

        public string? ApiService { get; set; }

        // Overwrites serverConfig's hysteresis fields from config on every startup instead of seeding once; false by default so it doesn't clobber admin-UI edits on restart.
        public bool ServerConfigReload { get; set; }

        // Fallback hysteresis defaults when ServerConfig:Hysteresis is missing entirely - same values firmware used to hardcode.
        public double HysteresisWaterLevel { get; set; } = 5.0;
        public double HysteresisTemperature { get; set; } = 1.0;
        public double HysteresisHumidity { get; set; } = 5.0;
        public double HysteresisLight { get; set; } = 20.0;

        // See Agrumy.Shared.Models.ServerConfig.BatteryLowThreshold/BatteryLowHysteresis for the dead-zone rule they feed into.
        public double BatteryLowThreshold { get; set; } = 20.0;
        public double BatteryLowHysteresis { get; set; } = 5.0;

        // See Agrumy.Shared.Models.ServerConfig.TankRefillThreshold/TankRefillHysteresis - same dead-zone rule, applied to TankCalculator's fill percent instead of Battery.
        public double TankRefillThreshold { get; set; } = 20.0;
        public double TankRefillHysteresis { get; set; } = 5.0;

        // WaterPump-only safety-limit defaults (30min run/5min cooldown); only seeds new devices, never retroactively changes existing DeviceConfigController rows.
        public int WaterPumpMaxRunSeconds { get; set; } = 1800;
        public int WaterPumpCooldownSeconds { get; set; } = 300;

        // How long a device's identical repeated event is ignored server-side.
        public int EventDedupeMinutes { get; set; } = 10;

        // How long a user must wait between "resend activation email" requests.
        public int ActivationResendCooldownMinutes { get; set; } = 10;

        // Hard ceiling is 32 (AgrumyFirmware DeviceModel.h's MAX_RULES), enforced in ServerConfigApiController.Update - this is only the default.
        public int MaxRulesPerZone { get; set; } = 10;

        // Off by default - UserRegistration rejects unknown tenant names until an admin opts in.
        public bool AllowSelfServiceTenantCreation { get; set; }

        // Off by default - gates the Tenant Management menu item alongside the GlobalAdmin role check.
        public bool TenantManagementEnabled { get; set; }

        // FirmwareLocalPath: relative to content root, null = FirmwareStorage.DefaultRelativePath. FirmwareGitHubRepository only seeds serverConfig - the admin page owns the live value.
        public string? FirmwareLocalPath { get; set; }

        // Relative to content root, null = FieldLogAttachmentStorage.DefaultRelativePath - same convention as FirmwareLocalPath.
        public string? FieldLogAttachmentLocalPath { get; set; }

        // Relative to content root, null = SatelliteStorage.DefaultRelativePath - same convention as FirmwareLocalPath. This is a regenerable PNG cache, not backed up like the others (D10 - the grid in the DB is the source of truth).
        public string? SatelliteLocalPath { get; set; }
        public string FirmwareGitHubRepository { get; set; } = "dopiskur/AgrumyFirmware";
        public string? FirmwareGitHubToken { get; set; }

        // Only seeds serverConfig on first creation - the admin page owns the live value; null/0 disables auto-refresh (manual-only).
        public int? FirmwareRefreshIntervalHours { get; set; } = 24;

        // Days of sensorData history to auto-purge; null = admin hasn't opted in, data just accumulates.
        public int? SensorDataRetentionDays { get; set; }

        // A secret, never exposed through ServerConfigApiController - unlike location/poll-interval/threshold, which are operational and live in Agrumy.Shared.Models.ServerConfig instead.
        public string? WeatherApiKey { get; set; }
        public int WeatherPollIntervalMinutes { get; set; } = 15;
        public double WeatherRainSkipThreshold { get; set; } = 50.0;

        // Radiative-frost factors - clear sky and calm wind let ground heat radiate away overnight, so a forecast near the threshold only counts as risky alongside low cloudiness/wind too.
        public int FrostLookaheadHours { get; set; } = 12;
        public double FrostTempThresholdC { get; set; } = 3.0;
        public double FrostCloudinessMaxPercent { get; set; } = 30.0;
        public double FrostWindMaxMetersPerSecond { get; set; } = 2.0;

        // Shared with Agrumy.Gateway's own GatewayOptions.Gateway.RegistrationSecret - proves a Register call declaring IsGateway:true actually comes from Agrumy.Gateway, not any client holding a valid user email+PIN. Null/empty means no gateway may self-register on this server.
        public string? GatewayRegistrationSecret { get; set; }

        public static AgrumySettings Bind(IConfiguration configuration) => new()
        {
            DefaultConnection = configuration.GetConnectionString("DefaultConnection"),
            DatabaseProvider = Environment.GetEnvironmentVariable("AGRUMY_DB_PROVIDER")
                ?? configuration.GetSection("Database:Provider").Value,
            JwtSecureKey = configuration.GetSection("JWT:SecureKey").Value,
            JwtIssuer = configuration.GetSection("JWT:Issuer").Value,
            JwtAudience = configuration.GetSection("JWT:Audience").Value,
            ApiService = configuration.GetSection("WebView:ApiService").Value,
            ServerConfigReload = bool.TryParse(configuration.GetSection("ServerConfig:Reload").Value, out var reload) && reload,
            HysteresisWaterLevel = ParseDoubleOr(configuration, "ServerConfig:Hysteresis:WaterLevel", 5.0),
            HysteresisTemperature = ParseDoubleOr(configuration, "ServerConfig:Hysteresis:Temperature", 1.0),
            HysteresisHumidity = ParseDoubleOr(configuration, "ServerConfig:Hysteresis:Humidity", 5.0),
            HysteresisLight = ParseDoubleOr(configuration, "ServerConfig:Hysteresis:Light", 20.0),
            BatteryLowThreshold = ParseDoubleOr(configuration, "ServerConfig:BatteryLowThreshold", 20.0),
            BatteryLowHysteresis = ParseDoubleOr(configuration, "ServerConfig:BatteryLowHysteresis", 5.0),
            TankRefillThreshold = ParseDoubleOr(configuration, "ServerConfig:TankRefillThreshold", 20.0),
            TankRefillHysteresis = ParseDoubleOr(configuration, "ServerConfig:TankRefillHysteresis", 5.0),
            WaterPumpMaxRunSeconds = ParseIntOr(configuration, "ServerConfig:WaterPumpMaxRunSeconds", 1800),
            WaterPumpCooldownSeconds = ParseIntOr(configuration, "ServerConfig:WaterPumpCooldownSeconds", 300),
            EventDedupeMinutes = ParseIntOr(configuration, "ServerConfig:EventDedupeMinutes", 10),
            ActivationResendCooldownMinutes = ParseIntOr(configuration, "ServerConfig:ActivationResendCooldownMinutes", 10),
            MaxRulesPerZone = ParseIntOr(configuration, "ServerConfig:MaxRulesPerZone", 10),
            AllowSelfServiceTenantCreation = ParseBoolOr(configuration, "ServerConfig:AllowSelfServiceTenantCreation", false),
            TenantManagementEnabled = ParseBoolOr(configuration, "ServerConfig:TenantManagementEnabled", false),
            FirmwareLocalPath = configuration.GetSection("Firmware:LocalPath").Value,
            FieldLogAttachmentLocalPath = configuration.GetSection("FieldLog:AttachmentLocalPath").Value,
            SatelliteLocalPath = configuration.GetSection("Satellite:LocalPath").Value,
            FirmwareGitHubRepository = configuration.GetSection("Firmware:GitHubRepository").Value is { Length: > 0 } repo ? repo : "dopiskur/AgrumyFirmware",
            FirmwareGitHubToken = configuration.GetSection("Firmware:GitHubToken").Value,
            FirmwareRefreshIntervalHours = ParseIntOrNull(configuration, "ServerConfig:FirmwareRefreshIntervalHours") ?? 24,
            SensorDataRetentionDays = ParseIntOrNull(configuration, "ServerConfig:SensorDataRetentionDays"),
            WeatherApiKey = configuration.GetSection("Weather:ApiKey").Value,
            WeatherPollIntervalMinutes = ParseIntOr(configuration, "ServerConfig:WeatherPollIntervalMinutes", 15),
            WeatherRainSkipThreshold = ParseDoubleOr(configuration, "ServerConfig:WeatherRainSkipThreshold", 50.0),
            FrostLookaheadHours = ParseIntOr(configuration, "ServerConfig:FrostLookaheadHours", 12),
            FrostTempThresholdC = ParseDoubleOr(configuration, "ServerConfig:FrostTempThresholdC", 3.0),
            FrostCloudinessMaxPercent = ParseDoubleOr(configuration, "ServerConfig:FrostCloudinessMaxPercent", 30.0),
            FrostWindMaxMetersPerSecond = ParseDoubleOr(configuration, "ServerConfig:FrostWindMaxMetersPerSecond", 2.0),
            GatewayRegistrationSecret = configuration.GetSection("Gateway:RegistrationSecret").Value,
        };

        // CurrentCulture would misparse "20.0" on a comma-decimal locale (e.g. hr-HR); InvariantCulture keeps "." as the decimal point regardless of OS locale.
        private static double ParseDoubleOr(IConfiguration configuration, string key, double fallback) =>
            double.TryParse(configuration.GetSection(key).Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

        private static int ParseIntOr(IConfiguration configuration, string key, int fallback) =>
            int.TryParse(configuration.GetSection(key).Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

        private static bool ParseBoolOr(IConfiguration configuration, string key, bool fallback) =>
            bool.TryParse(configuration.GetSection(key).Value, out var value) ? value : fallback;

        private static int? ParseIntOrNull(IConfiguration configuration, string key) =>
            int.TryParse(configuration.GetSection(key).Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
    }
}
