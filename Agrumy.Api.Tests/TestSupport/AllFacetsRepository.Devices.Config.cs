using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IDeviceRepository per-device config members - forwarded to the standalone EfDeviceRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<DeviceConfigSensor?> DeviceConfigSensorGetAsync(int? deviceConfigSensorID) => deviceRepository.DeviceConfigSensorGetAsync(deviceConfigSensorID);

        public Task<DeviceConfigController?> DeviceConfigControllerGetAsync(int? deviceConfigControllerID) => deviceRepository.DeviceConfigControllerGetAsync(deviceConfigControllerID);

        public Task<Device?> DeviceGetByDeviceConfigSensorIdAsync(int? deviceConfigSensorID) => deviceRepository.DeviceGetByDeviceConfigSensorIdAsync(deviceConfigSensorID);

        public Task<Device?> DeviceGetByDeviceConfigControllerIdAsync(int? deviceConfigControllerID) => deviceRepository.DeviceGetByDeviceConfigControllerIdAsync(deviceConfigControllerID);

        public Task<string?> DeviceConfigControllerUpdateAsync(int? idDevice, DeviceConfigController? deviceConfigController) =>
            deviceRepository.DeviceConfigControllerUpdateAsync(idDevice, deviceConfigController);

        public Task DeviceConfigSensorUpdateAsync(int? iDDevice, DeviceConfigSensor? deviceConfigSensor) => deviceRepository.DeviceConfigSensorUpdateAsync(iDDevice, deviceConfigSensor);
    }
}
