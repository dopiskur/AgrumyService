using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Long-term real-device rule experiments, plus the dual-write sensor/controller history tables recorded while one is active. Rule CRUD for an experiment's own scope lives on IDeviceFarmUnitRepository (RulesGetForExperimentAsync), same split as Simulation's RulesGetForSimulationAsync.
    public interface IExperimentRepository
    {
        Task<Experiment> ExperimentAddAsync(Experiment experiment);

        Task<IList<Experiment>> ExperimentsGetAsync(int? tenantID);

        Task<Experiment?> ExperimentGetByIdAsync(int idExperiment);

        /// No-op if already stopped - first write wins, same convention as SimulationSessionStopAsync.
        Task ExperimentStopAsync(int idExperiment);

        /// The id of whichever active experiment covers this zone via the Zone&gt;Unit&gt;Farm cascade (a Zone-level and its parent Unit/Farm-level experiment can coexist - the most specific one wins here, same precedence the rule hierarchy itself uses), or null if none - DeviceConfigBuilder's per-device lookup.
        Task<int?> ActiveExperimentIdForZoneAsync(int idDeviceFarmUnitZone);

        /// Batched tenant-wide zone id -> active experiment id map (same cascade as the single-zone lookup above, resolved for every zone at once) - RuleNotificationEvaluator's per-tenant lookup, avoiding an N+1 zone-by-zone query.
        Task<IDictionary<int, int>> ActiveExperimentIdsByZoneAsync(int tenantID);

        /// Appends one row per reading, tagged with idExperiment - called from EfSensorDataRepository.SensorDataPushAsync alongside its own normal dataSensor insert, never instead of it.
        Task SensorDataExperimentAddRangeAsync(int idExperiment, int deviceID, int tenantID, IReadOnlyList<SensorDataPushReading> readings);

        /// Appends one row per push entry, tagged with idExperiment - called from EfControllerDataRepository.ControllerDataPushAsync alongside its own dataController upsert. Unlike that upsert, this is a genuine log: every entry gets its own row.
        Task ControllerDataExperimentAddRangeAsync(int idExperiment, int deviceID, int tenantID, IList<ControllerDataPush> entries);

        /// Most recent samples first, capped by limit - a basic raw-data view, richer analysis deferred to a follow-up.
        Task<IList<ExperimentSensorSample>> ExperimentSensorSamplesGetAsync(int idExperiment, int limit);

        Task<IList<ExperimentControllerEvent>> ExperimentControllerEventsGetAsync(int idExperiment, int limit);
    }
}
