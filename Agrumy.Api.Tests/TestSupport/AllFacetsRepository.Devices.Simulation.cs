using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IDeviceRepository Simulation Mode members - forwarded to the standalone EfDeviceRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<DeviceSimulation?> DeviceSimulationGetAsync(int deviceID) => deviceRepository.DeviceSimulationGetAsync(deviceID);

        public Task DeviceSimulationSetAsync(int deviceID, DeviceSimulation value) => deviceRepository.DeviceSimulationSetAsync(deviceID, value);
    }
}
