namespace Agrumy.Shared.Models
{
    public class Tenant
    {
        public int? IDTenant { get; set; }
        public string? TenantName { get; set; }
        // IANA id; DeviceConfigBuilder uses it (falling back to AgrumySettings.ScheduleTimeZone, then UTC) to compute the UtcOffsetSeconds every device in this organization gets - null/empty means the organization hasn't set one of its own.
        public string? ScheduleTimeZone { get; set; }

        // Same per-organization-then-server-wide cascade as ScheduleTimeZone above - AstronomicalRuleResolver uses these for sunrise/sunset scheduling, falling back to ServerConfig.WeatherLocationLat/Lon when this organization hasn't set its own (a multi-organization install's organizations can sit at genuinely different physical sites).
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Organization-wide fail-closed switch - forces every relay in this organization off ahead of any rule, independent of a device's own RelayEnabled. Read-only here; the only writer is TenantEmergencyStopSetAsync via TenantApiController's dedicated endpoints, never TenantUpdateAsync's general rename/timezone path.
        public bool EmergencyStopActive { get; set; }

        // Per-organization override, null falls back to ServerConfig.RecycleBinRetentionDays, same cascade as ScheduleTimeZone/Latitude/Longitude above.
        public int? RecycleBinRetentionDays { get; set; }
    }

    /// One saved WiFi AP an organization's admin can hand to a newly discovered device instead of typing it in again on every Register; Password is omitted from any list response the UI uses just to pick one (see DiscoveryApiController.Register).
    public class TenantWifiConfig
    {
        public int IDTenantWifiConfig { get; set; }
        public int TenantID { get; set; }
        public string Ssid { get; set; } = "";
        public string? Password { get; set; }
    }

    /// An organization's own override of ServerConfig's alert thresholds - null on any field falls back to that ServerConfig value (same cascade convention as Tenant.ScheduleTimeZone/Latitude/Longitude), so each organization's Battery/Tank/problem-event alerts can differ from every other organization's and from the server-wide default. Self-scoped: GET/PUT /api/Tenant/AlertConfig always act on the caller's own organization.
    public class TenantAlertConfig
    {
        public bool? ProblemEventAlertsEnabled { get; set; }
        public int? ProblemEventExpiryHours { get; set; }
        public double? BatteryLowThreshold { get; set; }
        public double? BatteryLowHysteresis { get; set; }
        public double? TankRefillThreshold { get; set; }
        public double? TankRefillHysteresis { get; set; }
        public int? EventDedupeMinutes { get; set; }
    }

    /// Per-organization derived weather/frost state - WeatherEvaluator/FrostAlertEvaluator's own last result for this
    /// organization's resolved location (Tenant.Latitude/Longitude falling back to ServerConfig.WeatherLocationLat/Lon,
    /// same cascade as ScheduleTimeZone) - replaces the old single global ServerConfig row, since a per-organization
    /// location can no longer share one forecast state. Never null for a real organization - an organization with no row yet
    /// gets an all-default/false instance (same convention as TenantAlertConfigGetAsync). Self-scoped: GET
    /// /api/Tenant/WeatherState always acts on the caller's own organization.
    public class TenantWeatherState
    {
        public int TenantID { get; set; }
        public bool WeatherRainPredicted { get; set; }
        public DateTimeOffset? WeatherCheckedAtUtc { get; set; }
        public bool FrostPredicted { get; set; }
        public int? FrostPredictedHoursAhead { get; set; }
        public DateTimeOffset? FrostCheckedAtUtc { get; set; }
        /// Nearest-bucket reading from the same forecast call WeatherEvaluator already makes - feeds SensorMetric.OutdoorTemperature/OutdoorHumidity/OutdoorWind (RuleConditionEvaluator, "climate mirroring") as a live value alongside a zone's own SensorAverages, never a device's own SensorData.
        public double? OutdoorTemperatureC { get; set; }
        public double? OutdoorHumidityPercent { get; set; }
        public double? OutdoorWindSpeedMetersPerSecond { get; set; }
        public DateTimeOffset? OutdoorCheckedAtUtc { get; set; }
    }
}
