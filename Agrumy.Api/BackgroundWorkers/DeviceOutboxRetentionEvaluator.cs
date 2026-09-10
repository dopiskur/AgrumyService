using Agrumy.Api.Dal.Interface;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Only terminal (Executed/Expired) deviceOutbox rows age out - Pending/Acknowledged rows are never purged regardless of age.
    public sealed class DeviceOutboxRetentionEvaluator(IDeviceOutboxRepository outboxRepo)
    {
        private const int RetentionDays = 30;

        public Task RunOnceAsync(CancellationToken ct = default) =>
            outboxRepo.PurgeOldOutboxItemsAsync(DateTime.UtcNow.AddDays(-RetentionDays), ct);
    }
}
