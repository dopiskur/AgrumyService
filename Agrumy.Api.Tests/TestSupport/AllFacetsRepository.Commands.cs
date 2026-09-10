using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IDeviceOutboxRepository members - forwarded to the standalone EfDeviceOutboxRepository so RelationalIntegrationTests can drive many facets through one object.
    internal partial class AllFacetsRepository
    {
        public Task<bool> HasActiveOutboxItemAsync(int deviceId, CommandActionType type, DateTime utcNow) =>
            outboxRepository.HasActiveOutboxItemAsync(deviceId, type, utcNow);

        public Task<int?> AddOutboxItemAsync(int deviceId, CommandActionType type, DateTime issuedAt, DateTime expiresAt, string? payload = null) =>
            outboxRepository.AddOutboxItemAsync(deviceId, type, issuedAt, expiresAt, payload);

        public Task<IList<DeviceCommand>> GetPendingOutboxItemsAsync(int deviceId) => outboxRepository.GetPendingOutboxItemsAsync(deviceId);

        public Task ExpirePendingOutboxItemsAsync(int deviceId, DateTime utcNow) => outboxRepository.ExpirePendingOutboxItemsAsync(deviceId, utcNow);

        public Task<IList<DeviceCommand>> GetActiveProvisionCommandsAsync() => outboxRepository.GetActiveProvisionCommandsAsync();

        public Task<DeviceCommand?> GetOutboxItemByIdAsync(int commandId) => outboxRepository.GetOutboxItemByIdAsync(commandId);

        public Task SetOutboxItemStatusAsync(int commandId, CommandStatus status, DateTime? executedAt = null) =>
            outboxRepository.SetOutboxItemStatusAsync(commandId, status, executedAt);

        public Task ConsumePendingByTypeAsync(int deviceId, CommandActionType type, DateTime executedAtUtc) =>
            outboxRepository.ConsumePendingByTypeAsync(deviceId, type, executedAtUtc);

        public Task PurgeOldOutboxItemsAsync(DateTime issuedBeforeUtc, CancellationToken ct = default) => outboxRepository.PurgeOldOutboxItemsAsync(issuedBeforeUtc, ct);

        public Task<IList<DeviceCommand>> GetUnpublishedPendingItemsAsync(int batchSize) => outboxRepository.GetUnpublishedPendingItemsAsync(batchSize);

        public Task MarkPublishedAsync(int commandId, DateTime publishedAtUtc) => outboxRepository.MarkPublishedAsync(commandId, publishedAtUtc);
    }
}
