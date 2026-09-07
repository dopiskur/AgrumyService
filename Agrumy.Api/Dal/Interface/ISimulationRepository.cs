using api.Models;

namespace api.Dal.Interface
{
    /// The server-internal registry of which devices are fully virtual, used only by the Simulation admin endpoints and VirtualDeviceRunnerBackgroundService - never consulted by any device-facing endpoint.
    public interface ISimulationRepository
    {
        Task VirtualDeviceRegisterAsync(int deviceID);

        /// Every virtual device across every tenant that's also in a currently-active simulation session (roadmap #403) - the runner drives only these, not every registered virtual device unconditionally.
        Task<IList<int>> VirtualDeviceIdsGetAsync();

        /// Virtual device ids owned by tenantID, for the Web listing page (or every one when tenantID is null, GlobalAdmin's own-tenant-only rule still enforced by the caller). Unlike the parameterless overload, NOT limited to active-session membership - this is the raw registry for admin management (delete etc.), not the runner's own drive-list.
        Task<IList<int>> VirtualDeviceIdsGetAsync(int? tenantID);

        /// Deletes sensorData/controllerData/the registry row/the device itself (in that order) - a virtual device's synthetic telemetry has no historical value once it's gone, unlike a real device's (DeviceDeleteAsync alone does not touch sensorData).
        Task VirtualDeviceDeleteAsync(int deviceID, int? tenantID);

        // ---- Simulation sessions (roadmap #403) ----------------------------

        Task<SimulationSession> SimulationSessionAddAsync(SimulationSession session);

        /// Every session for tenantID (or every tenant when null, caller's own Global-admin check already applied) - Devices left empty, same "list is cheap" convention as the rest of this codebase's list/detail pairs.
        Task<IList<SimulationSession>> SimulationSessionsGetAsync(int? tenantID);

        /// Devices populated - the one place that costs an extra join, only paid on the single-session detail fetch.
        Task<SimulationSession?> SimulationSessionGetByIdAsync(int idSimulationSession);

        /// No-ops if already stopped - StoppedAtUtc is set once, first write wins, never overwritten by a later call (an explicit stop racing the expiry evaluator must not clobber whichever timestamp landed first).
        Task SimulationSessionStopAsync(int idSimulationSession);

        /// False (and adds nothing) if deviceID is already a member of a DIFFERENT currently-active session - a device can only ever be simulated by one session at a time.
        Task<bool> SimulationSessionDeviceAddAsync(int idSimulationSession, int deviceID);

        Task SimulationSessionDeviceRemoveAsync(int idSimulationSession, int deviceID);

        /// The id of whichever active (not stopped, not past ExpiresAtUtc) session currently owns deviceID, or null if it's in none - the "already spoken for" check SimulationSessionDeviceAddAsync itself uses, also useful for the Web UI to explain why an add was rejected.
        Task<int?> DeviceActiveSimulationSessionIdGetAsync(int deviceID);

        /// Sessions past ExpiresAtUtc but not yet StoppedAtUtc, Devices populated - SimulationSessionExpiryEvaluator's own worklist.
        Task<IList<SimulationSession>> SimulationSessionsExpiredButActiveGetAsync(DateTimeOffset nowUtc);
    }
}
