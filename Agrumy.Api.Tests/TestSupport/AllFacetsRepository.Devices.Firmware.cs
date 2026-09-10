using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IDeviceRepository's legacy board-less OTA lookup - forwarded to the standalone EfDeviceRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<DeviceFirmware?> DeviceFirmwareLatestGetAsync(int? deviceTypeID) => deviceRepository.DeviceFirmwareLatestGetAsync(deviceTypeID);
    }
}
