using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// DevEUI&lt;-&gt;device mapping and gateway listing for Agrumy.Gateway's LoRaGateway profile and its admin UI - gateway device rows themselves live in the ordinary device table (IDeviceRepository).
    public interface IGatewayRepository
    {
        /// Every device row with IsGateway=true, across every organization - gateways are install-wide infrastructure, not organization data.
        Task<IList<Device>> GatewayDevicesGetAllAsync();

        /// One gateway's DevEUI mappings, without the mapped device's ApiKey - the admin list view, not what Gateway itself fetches to build its forwarding cache.
        Task<IList<GatewayDeviceMapping>> GatewayDeviceMappingsGetAsync(int idGatewayDevice);

        /// Same rows as above, WITH each mapped device's ApiKey - only ever served to the owning gateway itself, never to the admin UI.
        Task<IList<GatewayDeviceMapping>> GatewayDeviceMappingsWithSecretsGetAsync(int idGatewayDevice);

        /// False (no-op) if idDevice doesn't exist, belongs to a different organization than gatewayTenantId, or DevEUI is already mapped for this gateway - the unique index is the real guard for the last case, this is just a friendlier failure than a raw constraint exception.
        Task<bool> GatewayDeviceMappingAddAsync(int idGatewayDevice, string devEUI, int idDevice, int? gatewayTenantId);

        Task<bool> GatewayDeviceMappingDeleteAsync(int idGatewayDeviceMapping, int idGatewayDevice);
    }
}
