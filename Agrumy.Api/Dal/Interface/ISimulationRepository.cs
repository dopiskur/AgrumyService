using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// The server-internal registry of which devices are fully virtual, used only by the Simulation admin endpoints and VirtualDeviceRunnerBackgroundService - never consulted by any device-facing endpoint.
    public interface ISimulationRepository
    {
        Task VirtualDeviceRegisterAsync(int deviceID);

        /// Every virtual device across every tenant that's also in a currently-active simulation session - the runner drives only these, not every registered virtual device unconditionally.
        Task<IList<int>> VirtualDeviceIdsGetAsync();

        /// Virtual device ids owned by tenantID, for the Web listing page (or every one when tenantID is null, GlobalAdmin's own-tenant-only rule still enforced by the caller). Unlike the parameterless overload, NOT limited to active-session membership - this is the raw registry for admin management (delete etc.), not the runner's own drive-list.
        Task<IList<int>> VirtualDeviceIdsGetAsync(int? tenantID);

        /// Deletes sensorData/controllerData/the registry row/the device itself (in that order) - a virtual device's synthetic telemetry has no historical value once it's gone, unlike a real device's (DeviceDeleteAsync alone does not touch sensorData).
        Task VirtualDeviceDeleteAsync(int deviceID, int? tenantID);

        // ---- Simulation sessions ----------------------------

        /// Name only, StartedAtUtc/ExpiresAtUtc stay null until SimulationSessionStartAsync.
        /// quotaCheckAsync (when given) runs inside the same Serializable transaction as the insert, so a concurrent Add can't slip past a stale count - see Agrumy.Api.Quota.QuotaGuard.
        Task<SimulationSession> SimulationSessionAddAsync(SimulationSession session, Func<Task<string?>>? quotaCheckAsync = null);

        /// Sets a fresh StartedAtUtc/ExpiresAtUtc window from now and clears StoppedAtUtc - same call whether this is the session's first start or a later Resume.
        Task SimulationSessionStartAsync(int idSimulationSession, int durationMinutes);

        /// Removes the session and its device memberships outright - caller is responsible for turning off any member physical device's sensor override first (same as SimulationSessionStopAsync's own cleanup).
        Task SimulationSessionDeleteAsync(int idSimulationSession);

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

        /// Zone or Parcel id (Open-Field's own leaf level, same dictionary) -> the active session id owning at least one member device assigned there, for every zone/parcel in tenantID - RuleNotificationEvaluator's per-leaf "which simulation-scoped rules (if any) apply here" lookup. A leaf with two overlapping sessions' devices (should not normally happen - AddDeviceToSession already rejects a device already active elsewhere) resolves to whichever session the query happens to return last, not both.
        Task<IDictionary<int, int>> ActiveSimulationSessionIdsByZoneAsync(int tenantID);

        // ---- Simulation groups - a whole Unit/Zone added together, one override value set fanned out to every member device's own DeviceSimulation. ----

        /// Persists the group then fans its override values out to every current member of ScopeID's Unit/Zone, adding each as a session member (or re-tagging an existing membership as belonging to this group) - a device already active in a DIFFERENT session is skipped, not a hard failure for the whole group.
        Task<SimulationGroup> SimulationGroupAddAsync(SimulationGroup group);

        Task<IList<SimulationGroup>> SimulationGroupsGetAsync(int idSimulationSession);

        Task<SimulationGroup?> SimulationGroupGetByIdAsync(int idSimulationGroup);

        /// Re-applies new override values to whichever devices currently belong to the group - does NOT re-resolve Unit/Zone membership, same snapshot-at-add-time convention as a single device's own add.
        Task SimulationGroupUpdateAsync(SimulationGroup group);

        /// Turns off every physical member's override first, then drops the membership rows and the group itself.
        Task SimulationGroupDeleteAsync(int idSimulationGroup);
    }
}
