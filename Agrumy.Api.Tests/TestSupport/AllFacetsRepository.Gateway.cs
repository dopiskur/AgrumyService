using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IGatewayRepository members - forwarded to the standalone EfGatewayRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<IList<Device>> GatewayDevicesGetAllAsync() => gatewayRepository.GatewayDevicesGetAllAsync();

        public Task<IList<GatewayDeviceMapping>> GatewayDeviceMappingsGetAsync(int idGatewayDevice) => gatewayRepository.GatewayDeviceMappingsGetAsync(idGatewayDevice);

        public Task<IList<GatewayDeviceMapping>> GatewayDeviceMappingsWithSecretsGetAsync(int idGatewayDevice) => gatewayRepository.GatewayDeviceMappingsWithSecretsGetAsync(idGatewayDevice);

        public Task<bool> GatewayDeviceMappingAddAsync(int idGatewayDevice, string devEUI, int idDevice, int? gatewayTenantId) =>
            gatewayRepository.GatewayDeviceMappingAddAsync(idGatewayDevice, devEUI, idDevice, gatewayTenantId);

        public Task<bool> GatewayDeviceMappingDeleteAsync(int idGatewayDeviceMapping, int idGatewayDevice) =>
            gatewayRepository.GatewayDeviceMappingDeleteAsync(idGatewayDeviceMapping, idGatewayDevice);
    }
}
