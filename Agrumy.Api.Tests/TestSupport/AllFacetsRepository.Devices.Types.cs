using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IDeviceRepository fixed-type-list members - forwarded to the standalone EfDeviceRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<IList<DeviceRole>> DeviceRoleGetAsync() => deviceRepository.DeviceRoleGetAsync();

        public Task<IList<DeviceType>> DeviceTypeGetAsync() => deviceRepository.DeviceTypeGetAsync();

        public Task<IList<DeviceTypeService>> DeviceTypeServiceGetAsync() => deviceRepository.DeviceTypeServiceGetAsync();

        public Task<IList<DeviceTypeRelay>> DeviceTypeRelayGetAsync() => deviceRepository.DeviceTypeRelayGetAsync();

        public Task<IList<DeviceTypeSensor>> DeviceTypeSensorGetAsync() => deviceRepository.DeviceTypeSensorGetAsync();
    }
}
