using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IExperimentRepository members - forwarded to the standalone EfExperimentRepository so RelationalIntegrationTests can drive many facets through one object, same pattern as AllFacetsRepository.Simulation.cs.
    internal partial class AllFacetsRepository
    {
        public Task<Experiment> ExperimentAddAsync(Experiment experiment) => experimentRepository.ExperimentAddAsync(experiment);

        public Task<IList<Experiment>> ExperimentsGetAsync(int? tenantID) => experimentRepository.ExperimentsGetAsync(tenantID);

        public Task<Experiment?> ExperimentGetByIdAsync(int idExperiment) => experimentRepository.ExperimentGetByIdAsync(idExperiment);

        public Task ExperimentStopAsync(int idExperiment) => experimentRepository.ExperimentStopAsync(idExperiment);

        public Task<int?> ActiveExperimentIdForZoneAsync(int idDeviceFarmUnitZone) => experimentRepository.ActiveExperimentIdForZoneAsync(idDeviceFarmUnitZone);

        public Task<int?> ActiveExperimentIdForParcelAsync(int idFarmOpenfieldCropParcel) => experimentRepository.ActiveExperimentIdForParcelAsync(idFarmOpenfieldCropParcel);

        public Task<IDictionary<int, int>> ActiveExperimentIdsByZoneAsync(int tenantID) => experimentRepository.ActiveExperimentIdsByZoneAsync(tenantID);

        public Task SensorDataExperimentAddRangeAsync(int idExperiment, int deviceID, int tenantID, IReadOnlyList<SensorDataPushReading> readings) =>
            experimentRepository.SensorDataExperimentAddRangeAsync(idExperiment, deviceID, tenantID, readings);

        public Task ControllerDataExperimentAddRangeAsync(int idExperiment, int deviceID, int tenantID, IList<ControllerDataPush> entries) =>
            experimentRepository.ControllerDataExperimentAddRangeAsync(idExperiment, deviceID, tenantID, entries);

        public Task<IList<ExperimentSensorSample>> ExperimentSensorSamplesGetAsync(int idExperiment, int limit) => experimentRepository.ExperimentSensorSamplesGetAsync(idExperiment, limit);

        public Task<IList<ExperimentControllerEvent>> ExperimentControllerEventsGetAsync(int idExperiment, int limit) => experimentRepository.ExperimentControllerEventsGetAsync(idExperiment, limit);
    }
}
