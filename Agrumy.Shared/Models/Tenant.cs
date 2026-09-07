namespace api.Models
{
    public class Tenant
    {
        public int? IDTenant { get; set; }
        public string? TenantName { get; set; }
        // IANA id; DeviceConfigBuilder uses it (falling back to AgrumySettings.ScheduleTimeZone, then UTC) to compute the UtcOffsetSeconds every device in this tenant gets - null/empty means the tenant hasn't set one of its own.
        public string? ScheduleTimeZone { get; set; }

        // Same per-tenant-then-server-wide cascade as ScheduleTimeZone above (roadmap #396(6)) - AstronomicalRuleResolver uses these for sunrise/sunset scheduling, falling back to ServerConfig.WeatherLocationLat/Lon when this tenant hasn't set its own (a multi-tenant install's tenants can sit at genuinely different physical sites).
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // Tenant-wide fail-closed switch (roadmap #230) - forces every relay in this tenant off ahead of any rule, independent of a device's own RelayEnabled. Read-only here; the only writer is TenantEmergencyStopSetAsync via TenantApiController's dedicated endpoints, never TenantUpdateAsync's general rename/timezone path.
        public bool EmergencyStopActive { get; set; }
    }

    /// One saved WiFi AP a tenant's admin can hand to a newly discovered device instead of typing it in again on every Register; Password is omitted from any list response the UI uses just to pick one (see DiscoveryApiController.Register).
    public class TenantWifiConfig
    {
        public int IDTenantWifiConfig { get; set; }
        public int TenantID { get; set; }
        public string Ssid { get; set; } = "";
        public string? Password { get; set; }
    }
}
