namespace Agrumy.Shared.Models
{
    /// Per-tenant hard caps, Global Admin-only, exempt for IDTenant=0 (see TenantApiController/TenantQuotaApiController) - ingest-volume limits, never feature gates: rule engine/notifications/dashboard/sensor catalog stay fully open at every level this defines.
    public class TenantQuota
    {
        public int IDTenant { get; set; }
        // Tenant-wide, not per-Farm/Unit/Zone - the only limit that directly costs (telemetry rows, broker connections, LoRa slots), checked at Register (a device becomes real the moment it lands in the devices table, not when an admin later assigns it a Farm/Unit/Zone).
        public int MaxDevices { get; set; }
        public int MaxFarms { get; set; }
        public int MaxUnits { get; set; }
        public int MaxZones { get; set; }
        // Per device, not tenant-wide - DeviceConfigController.Relays.Count on any single device.
        public int MaxControllersPerDevice { get; set; }
        // Per device, not tenant-wide - count of enabled Sensor* fields on DeviceConfigSensor for any single device.
        public int MaxSensorsPerDevice { get; set; }
        public bool MqttEnabled { get; set; }
        public bool LoRaEnabled { get; set; }
        public bool GatewayEnabled { get; set; }
        public int MinSensorIntervalMinutes { get; set; }
        public int MaxDataRetentionDays { get; set; }
        // Replaces Tenant.RecycleBinRetentionDays' tenant-admin self-config while a quota governs this tenant, not merely a cap on it.
        public int RecycleBinRetentionDays { get; set; }
        public int MaxUsers { get; set; }
        public int MaxSimulations { get; set; }

        public static TenantQuota Default(int idTenant) => new()
        {
            IDTenant = idTenant,
            MaxDevices = 5,
            MaxFarms = 1,
            MaxUnits = 1,
            MaxZones = 3,
            MaxControllersPerDevice = 1,
            MaxSensorsPerDevice = 3,
            MqttEnabled = false,
            LoRaEnabled = false,
            GatewayEnabled = false,
            MinSensorIntervalMinutes = 5,
            MaxDataRetentionDays = 90,
            RecycleBinRetentionDays = 30,
            MaxUsers = 1,
            MaxSimulations = 1,
        };
    }
}
