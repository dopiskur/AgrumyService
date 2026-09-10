using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Catch-all MQTT dispatch sweep for every deviceOutbox row nobody has published yet - the Dal-layer ConfigChanged inserts (EfDeviceRepository etc.) have no synchronous MQTT push of their own since Dal repositories don't depend on IMqttCommandPublisher, so this is the only path that ever pushes them; also a safety net for anything a synchronous issue-path call missed.
    public sealed class DeviceOutboxDispatchEvaluator(IDeviceOutboxRepository outboxRepo, IDeviceRepository deviceRepo, IMqttCommandPublisher mqttPublisher)
    {
        private const int BatchSize = 200;

        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            foreach (DeviceCommand item in await outboxRepo.GetUnpublishedPendingItemsAsync(BatchSize))
            {
                Device? device = await deviceRepo.DeviceGetByIdAsync(item.DeviceID);
                if (device != null)
                {
                    await mqttPublisher.PublishAsync(device, new PendingCommand
                    {
                        IDDeviceCommand = item.IDDeviceCommand,
                        ActionType = item.ActionType,
                        ExpiresAt = item.ExpiresAt,
                        Payload = item.Payload,
                    }, ct);
                }
                // Marked regardless of outcome (missing device, MQTT disabled, broker failure) - best-effort, matches MqttCommandPublisher's own "never blocks/never retries" contract.
                await outboxRepo.MarkPublishedAsync(item.IDDeviceCommand, DateTime.UtcNow);
            }
        }
    }
}
