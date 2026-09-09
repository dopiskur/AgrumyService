namespace Agrumy.Shared.Models
{
    /// Canonical deviceTypeService IDs (device.DeviceTypeServiceID, DeviceRegistration.ServiceType) - must match AgrumyFirmware's ServiceTypeIds:: constants exactly, same cross-repo convention as SensorTypeIds.
    public static class DeviceServiceTypeIds
    {
        public const int Http = 0;
        public const int Https = 1;
        public const int Mqtt = 2;
    }
}
