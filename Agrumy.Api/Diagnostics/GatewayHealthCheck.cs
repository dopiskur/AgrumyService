using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Agrumy.Api.Diagnostics
{
    /// Reflects whether ANY registered Agrumy.Gateway device is currently online (same heartbeat mechanism as any other device, DeviceFleetStatus.ComputeOnline) - Agrumy.Api has no visibility into a gateway's own internal ChirpStack/LoRa transport (that state lives entirely inside the separate Agrumy.Gateway process), so this is the closest available proxy for both "Gateway service" and "LoRaWAN/ChirpStack" connectivity on this card.
    internal sealed class GatewayHealthCheck(IGatewayRepository gatewayRepo, IDeviceRepository deviceRepo) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            IList<Device> gateways = await gatewayRepo.GatewayDevicesGetAllAsync();
            if (gateways.Count == 0)
            {
                return HealthCheckResult.Degraded("Gateway support is enabled but no gateway device is registered yet.");
            }

            IList<DeviceFleetStatus> fleet = await deviceRepo.DeviceFleetGetAsync(null);
            var gatewayIds = gateways.Select(g => g.IDDevice).ToHashSet();
            int onlineCount = fleet.Count(d => d.IDDevice.HasValue && gatewayIds.Contains(d.IDDevice) && d.Online);

            return onlineCount > 0
                ? HealthCheckResult.Healthy($"{onlineCount}/{gateways.Count} gateway device(s) online.")
                : HealthCheckResult.Unhealthy($"0/{gateways.Count} gateway device(s) online.");
        }
    }
}
