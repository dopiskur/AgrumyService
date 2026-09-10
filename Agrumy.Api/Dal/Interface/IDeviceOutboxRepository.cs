using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Outbox facet: raw deviceOutbox CRUD only - dedup, fan-out, and FIFO ordering live in DeviceOutboxService above this facet.
    public interface IDeviceOutboxRepository
    {
        /// Every still-active (Pending/Acknowledged) item for this device matching Type, used for the per-(device, Type) dedup check - excludes expired-but-not-yet-marked rows via the ExpiresAt filter, not Status alone.
        Task<bool> HasActiveOutboxItemAsync(int deviceId, CommandActionType type, DateTime utcNow);

        /// Creates one Pending outbox row - null return means a unique-constraint rejection (another request already has an active item for this pair, or - for ConfigChanged/HardReset - one is already pending), treated by the caller as a dedup skip. payload is only ever set for ProvisionDevice (Agrumy.Shared.Models.DiscoveryProvisionPayload, JSON).
        Task<int?> AddOutboxItemAsync(int deviceId, CommandActionType type, DateTime issuedAt, DateTime expiresAt, string? payload = null);

        /// Every Pending item for this device, oldest first - DeviceOutboxService picks the first not (yet) expired and lazily expires any that are.
        Task<IList<DeviceCommand>> GetPendingOutboxItemsAsync(int deviceId);

        /// Bulk-marks every Pending, already-expired item for this device Expired in one statement - the N-row-write alternative to expiring them one at a time in a loop.
        Task ExpirePendingOutboxItemsAsync(int deviceId, DateTime utcNow);

        /// Every still-active (Pending/Acknowledged) ProvisionDevice item across every device - DeviceRegistration's match-by-DiscoveredApMac scans this small set since the target device (not yet registered) has no DeviceID to filter by.
        Task<IList<DeviceCommand>> GetActiveProvisionCommandsAsync();

        Task<DeviceCommand?> GetOutboxItemByIdAsync(int commandId);

        Task SetOutboxItemStatusAsync(int commandId, CommandStatus status, DateTime? executedAt = null);

        /// Marks every still-Pending item of this Type for this device Executed in one statement - used to consume a ConfigChanged/HardReset signal once it has actually been acted on (config sent / Reset field included).
        Task ConsumePendingByTypeAsync(int deviceId, CommandActionType type, DateTime executedAtUtc);

        /// Deletes every terminal-status (Executed/Expired) row issued before the cutoff - deviceOutbox has no other cleanup mechanism.
        Task PurgeOldOutboxItemsAsync(DateTime issuedBeforeUtc, CancellationToken ct = default);

        /// Every still-Pending, not-yet-dispatched, not-yet-expired row across all devices, oldest first, capped at batchSize - DeviceOutboxDispatchEvaluator's sweep query (catches ConfigChanged/HardReset rows a Dal repo inserted with no synchronous MQTT push, plus anything the synchronous issue-path missed).
        Task<IList<DeviceCommand>> GetUnpublishedPendingItemsAsync(int batchSize);

        /// Marks one row as dispatch-attempted (best-effort, regardless of whether the MQTT push actually succeeded) so the sweep never re-publishes it.
        Task MarkPublishedAsync(int commandId, DateTime publishedAtUtc);
    }
}
