using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// ISimulationRepository members - forwarded to the standalone EfSimulationRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task VirtualDeviceRegisterAsync(int deviceID) => simulationRepository.VirtualDeviceRegisterAsync(deviceID);

        public Task<IList<int>> VirtualDeviceIdsGetAsync() => simulationRepository.VirtualDeviceIdsGetAsync();

        public Task<IList<int>> VirtualDeviceIdsGetAsync(int? tenantID) => simulationRepository.VirtualDeviceIdsGetAsync(tenantID);

        public Task VirtualDeviceDeleteAsync(int deviceID, int? tenantID) => simulationRepository.VirtualDeviceDeleteAsync(deviceID, tenantID);

        public Task<SimulationSession> SimulationSessionAddAsync(SimulationSession session, Func<Task<string?>>? quotaCheckAsync = null) => simulationRepository.SimulationSessionAddAsync(session, quotaCheckAsync);

        public Task SimulationSessionStartAsync(int idSimulationSession, int durationMinutes) => simulationRepository.SimulationSessionStartAsync(idSimulationSession, durationMinutes);

        public Task SimulationSessionDeleteAsync(int idSimulationSession) => simulationRepository.SimulationSessionDeleteAsync(idSimulationSession);

        public Task<IList<SimulationSession>> SimulationSessionsGetAsync(int? tenantID) => simulationRepository.SimulationSessionsGetAsync(tenantID);

        public Task<SimulationSession?> SimulationSessionGetByIdAsync(int idSimulationSession) => simulationRepository.SimulationSessionGetByIdAsync(idSimulationSession);

        public Task SimulationSessionStopAsync(int idSimulationSession) => simulationRepository.SimulationSessionStopAsync(idSimulationSession);

        public Task<bool> SimulationSessionDeviceAddAsync(int idSimulationSession, int deviceID) => simulationRepository.SimulationSessionDeviceAddAsync(idSimulationSession, deviceID);

        public Task SimulationSessionDeviceRemoveAsync(int idSimulationSession, int deviceID) => simulationRepository.SimulationSessionDeviceRemoveAsync(idSimulationSession, deviceID);

        public Task<int?> DeviceActiveSimulationSessionIdGetAsync(int deviceID) => simulationRepository.DeviceActiveSimulationSessionIdGetAsync(deviceID);

        public Task<IList<SimulationSession>> SimulationSessionsExpiredButActiveGetAsync(DateTimeOffset nowUtc) => simulationRepository.SimulationSessionsExpiredButActiveGetAsync(nowUtc);

        public Task<IDictionary<int, int>> ActiveSimulationSessionIdsByZoneAsync(int tenantID) => simulationRepository.ActiveSimulationSessionIdsByZoneAsync(tenantID);

        public Task<SimulationGroup> SimulationGroupAddAsync(SimulationGroup group) => simulationRepository.SimulationGroupAddAsync(group);

        public Task<IList<SimulationGroup>> SimulationGroupsGetAsync(int idSimulationSession) => simulationRepository.SimulationGroupsGetAsync(idSimulationSession);

        public Task<SimulationGroup?> SimulationGroupGetByIdAsync(int idSimulationGroup) => simulationRepository.SimulationGroupGetByIdAsync(idSimulationGroup);

        public Task SimulationGroupUpdateAsync(SimulationGroup group) => simulationRepository.SimulationGroupUpdateAsync(group);

        public Task SimulationGroupDeleteAsync(int idSimulationGroup) => simulationRepository.SimulationGroupDeleteAsync(idSimulationGroup);
    }
}
