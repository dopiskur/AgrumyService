using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IDiscoveryRepository members - forwarded to the standalone EfDiscoveryRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task DiscoveryReportAddAsync(int scanningDeviceId, string discoveredApMac, int? rssi) =>
            discoveryRepository.DiscoveryReportAddAsync(scanningDeviceId, discoveredApMac, rssi);

        public Task<IList<DiscoveryResult>> DiscoveryResultsGetAsync(int? tenantId, int? unitId, int? zoneId, int? parcelId = null) =>
            discoveryRepository.DiscoveryResultsGetAsync(tenantId, unitId, zoneId, parcelId);

        public Task<DiscoveryResult?> DiscoveryResultGetAsync(string discoveredApMac, int? tenantId) =>
            discoveryRepository.DiscoveryResultGetAsync(discoveredApMac, tenantId);
    }
}
