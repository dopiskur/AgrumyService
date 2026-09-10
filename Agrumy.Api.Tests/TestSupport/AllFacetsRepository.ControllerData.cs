using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IControllerDataRepository members - forwarded to the standalone EfControllerDataRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task ControllerDataPushAsync(int deviceID, int tenantID, IList<ControllerDataPush> entries) =>
            controllerDataRepository.ControllerDataPushAsync(deviceID, tenantID, entries);

        public Task<IList<ControllerDataStatus>> ControllerDataGetAsync(int deviceID) => controllerDataRepository.ControllerDataGetAsync(deviceID);
    }
}
