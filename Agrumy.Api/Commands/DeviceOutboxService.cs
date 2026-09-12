using System.Text.Json;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Commands
{
    /// Success (>=1 command created) allows Message to be null; AllDuplicates and TargetNotFound always carry one so the API controller can pass it straight through as the error body.
    public enum IssueCommandOutcome
    {
        Success,
        AllDuplicates,
        TargetNotFound,
    }

    public sealed record IssueCommandResult(IssueCommandOutcome Outcome, IReadOnlyList<int> CreatedCommandIds, string? Message = null);

    /// One device's pending outbox state as seen by a Config poll - Actionable is what rides in DeviceConfig.PendingCommand (needs an explicit Ack/Executed from firmware); ConfigChangePending/HardResetPending are synthetic signals consumed the moment they're acted on, never surfaced to firmware as a command.
    public sealed record PendingItems(PendingCommand? Actionable, bool ConfigChangePending, bool HardResetPending);

    /// Dedup, target resolution/fan-out, FIFO pending-item lookup, and ack/execute state transitions over the single deviceOutbox table; no background worker for the queue itself - expiry is lazy, applied the moment a stale Pending row is next looked at. DeviceOutboxDispatchBackgroundService separately sweeps for anything still owed an MQTT dispatch attempt.
    public sealed class DeviceOutboxService(IDeviceOutboxRepository outboxRepo, IDeviceRepository deviceRepo, IDeviceFarmUnitRepository unitRepo, IFarmParcelRepository farmParcelRepo, IMqttCommandPublisher mqttPublisher)
    {
        private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(30);
        // A hard-reset intent must survive until the device actually checks in (which may be days away for a long-sleep node), not expire like an ordinary 30-minute command.
        private static readonly TimeSpan HardResetExpiry = TimeSpan.FromDays(365);

        /// Resolves TargetType/TargetId to the actual device(s), then per-device dedup against an active unexpired command of that ActionType; a fan-out is Success unless EVERY resolved device already had one, which is AllDuplicates.
        public async Task<IssueCommandResult> IssueCommandAsync(CommandTargetType targetType, int targetId, CommandActionType actionType)
        {
            IList<Device> targets;
            string notFoundMessage;
            switch (targetType)
            {
                case CommandTargetType.Device:
                    Device? device = await deviceRepo.DeviceGetByIdAsync(targetId);
                    targets = device == null ? [] : [device];
                    notFoundMessage = $"Device {targetId} not found.";
                    break;
                case CommandTargetType.Zone:
                    // A zone has at most one controller - null means it genuinely has none, which must surface as an error, not a silent zero-created no-op.
                    Device? controller = await unitRepo.DeviceFarmUnitZoneGetControllerAsync(targetId);
                    targets = controller == null ? [] : [controller];
                    notFoundMessage = $"Zone {targetId} has no controller assigned.";
                    break;
                case CommandTargetType.Unit:
                    targets = await unitRepo.DeviceFarmUnitGetControllersAsync(targetId);
                    notFoundMessage = $"Unit {targetId} has no controllers across any of its zones.";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(targetType), targetType, null);
            }

            if (targets.Count == 0)
            {
                return new IssueCommandResult(IssueCommandOutcome.TargetNotFound, [], notFoundMessage);
            }

            return await IssueToTargetsAsync(targets, actionType);
        }

        /// Fans one actionType to every device in the organization - used by organization-wide events (e.g. emergency stop) that have no single Zone/Unit/Device target, same dedup/fan-out tail via IssueToTargetsAsync.
        public async Task<IssueCommandResult> IssueTenantWideCommandAsync(int tenantId, CommandActionType actionType)
        {
            IList<Device> targets = await deviceRepo.DevicesGetAsync(tenantId);
            if (targets.Count == 0)
            {
                return new IssueCommandResult(IssueCommandOutcome.TargetNotFound, [], $"Tenant {tenantId} has no devices.");
            }
            return await IssueToTargetsAsync(targets, actionType);
        }

        /// Fans ScanForDevices to every sensor-only device in scope (zone, else unit, else farm, else organization-wide) - a different target-resolution rule than IssueCommandAsync's, same dedup/fan-out tail via IssueToTargetsAsync.
        public async Task<IssueCommandResult> IssueScanCommandAsync(int? tenantId, int? unitId, int? zoneId, int? farmId = null, int? parcelId = null)
        {
            IList<Device> targets;
            string notFoundMessage;
            if (zoneId is int zid)
            {
                targets = await unitRepo.DeviceFarmUnitZoneGetSensorsAsync(zid);
                notFoundMessage = $"Zone {zid} has no sensor-only devices.";
            }
            else if (parcelId is int pid)
            {
                targets = await farmParcelRepo.FarmParcelZoneGetSensorsAsync(pid);
                notFoundMessage = $"Parcel {pid} has no sensor-only devices.";
            }
            else if (unitId is int uid)
            {
                targets = await unitRepo.DeviceFarmUnitGetSensorsAsync(uid);
                notFoundMessage = $"Unit {uid} has no sensor-only devices across any of its zones.";
            }
            else if (farmId is int fid)
            {
                targets = await unitRepo.DeviceFarmGetSensorsAsync(fid);
                notFoundMessage = $"Farm {fid} has no sensor-only devices across any of its units.";
            }
            else
            {
                targets = await deviceRepo.DevicesSensorOnlyGetAsync(tenantId);
                notFoundMessage = "No sensor-only devices found.";
            }

            if (targets.Count == 0)
            {
                return new IssueCommandResult(IssueCommandOutcome.TargetNotFound, [], notFoundMessage);
            }

            return await IssueToTargetsAsync(targets, CommandActionType.ScanForDevices);
        }

        /// Issues a ProvisionDevice command to exactly one device (the Register flow's winning scanning device), carrying payloadJson - see Agrumy.Shared.Models.DiscoveryProvisionPayload.
        public async Task<IssueCommandResult> IssueProvisionCommandAsync(int deviceId, string payloadJson)
        {
            DateTime utcNow = DateTime.UtcNow;
            if (await outboxRepo.HasActiveOutboxItemAsync(deviceId, CommandActionType.ProvisionDevice, utcNow))
            {
                return new IssueCommandResult(IssueCommandOutcome.AllDuplicates, [], "A provisioning command is already pending for this device.");
            }
            DateTime expiresAt = utcNow + DefaultExpiry;
            if (await outboxRepo.AddOutboxItemAsync(deviceId, CommandActionType.ProvisionDevice, utcNow, expiresAt, payloadJson) is int newCommandId)
            {
                Device? target = await deviceRepo.DeviceGetByIdAsync(deviceId);
                if (target != null)
                {
                    await PublishAndMarkAsync(target, new PendingCommand
                    {
                        IDDeviceCommand = newCommandId,
                        ActionType = CommandActionType.ProvisionDevice,
                        ExpiresAt = expiresAt,
                        Payload = payloadJson,
                    });
                }
                return new IssueCommandResult(IssueCommandOutcome.Success, [newCommandId]);
            }
            return new IssueCommandResult(IssueCommandOutcome.AllDuplicates, [], "A provisioning command is already pending for this device.");
        }

        /// Issues an UpdateWifiCredentials command to one already-registered device - same single-device dedup as IssueToTargetsAsync, but with a payload (new Ssid/WifiPassword) so it can't reuse that shared helper.
        public async Task<IssueCommandResult> IssueWifiUpdateCommandAsync(int deviceId, string ssid, string wifiPassword)
        {
            DateTime utcNow = DateTime.UtcNow;
            if (await outboxRepo.HasActiveOutboxItemAsync(deviceId, CommandActionType.UpdateWifiCredentials, utcNow))
            {
                return new IssueCommandResult(IssueCommandOutcome.AllDuplicates, [], "A WiFi update is already pending for this device.");
            }
            DateTime expiresAt = utcNow + DefaultExpiry;
            string payloadJson = JsonSerializer.Serialize(new WifiUpdatePayload { Ssid = ssid, WifiPassword = wifiPassword });
            if (await outboxRepo.AddOutboxItemAsync(deviceId, CommandActionType.UpdateWifiCredentials, utcNow, expiresAt, payloadJson) is int newCommandId)
            {
                Device? target = await deviceRepo.DeviceGetByIdAsync(deviceId);
                if (target != null)
                {
                    await PublishAndMarkAsync(target, new PendingCommand
                    {
                        IDDeviceCommand = newCommandId,
                        ActionType = CommandActionType.UpdateWifiCredentials,
                        ExpiresAt = expiresAt,
                        Payload = payloadJson,
                    });
                }
                return new IssueCommandResult(IssueCommandOutcome.Success, [newCommandId]);
            }
            return new IssueCommandResult(IssueCommandOutcome.AllDuplicates, [], "A WiFi update is already pending for this device.");
        }

        /// Finds the active ProvisionDevice command whose payload targeted this MacAddress and marks it Executed so a later re-registration of the same mac never reapplies a stale intent; null when this mac never went through that discovery/registration flow.
        public async Task<DiscoveryProvisionPayload?> ConsumePendingProvisionAsync(string macAddress)
        {
            foreach (var candidate in await outboxRepo.GetActiveProvisionCommandsAsync())
            {
                if (candidate.Payload is null)
                {
                    continue;
                }
                DiscoveryProvisionPayload? payload;
                try
                {
                    payload = JsonSerializer.Deserialize<DiscoveryProvisionPayload>(candidate.Payload);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (payload != null && string.Equals(payload.DiscoveredApMac, macAddress, StringComparison.OrdinalIgnoreCase))
                {
                    await outboxRepo.SetOutboxItemStatusAsync(candidate.IDDeviceCommand, CommandStatus.Executed, DateTime.UtcNow);
                    return payload;
                }
            }
            return null;
        }

        /// Per-(device, ActionType) dedup then insert, shared by every fan-out entry point above.
        private async Task<IssueCommandResult> IssueToTargetsAsync(IList<Device> targets, CommandActionType actionType)
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime expiresAt = utcNow + DefaultExpiry;
            var created = new List<int>();

            foreach (var target in targets)
            {
                if (target.IDDevice is not int deviceId)
                {
                    continue;
                }
                if (await outboxRepo.HasActiveOutboxItemAsync(deviceId, actionType, utcNow))
                {
                    continue; // this one device is skipped, not the whole batch
                }
                // AddOutboxItemAsync can still return null here - the DB unique index closes the race, this in-memory check is only a fast-path.
                if (await outboxRepo.AddOutboxItemAsync(deviceId, actionType, utcNow, expiresAt) is int newCommandId)
                {
                    created.Add(newCommandId);
                    await PublishAndMarkAsync(target, new PendingCommand
                    {
                        IDDeviceCommand = newCommandId,
                        ActionType = actionType,
                        ExpiresAt = expiresAt,
                    });
                }
            }

            return created.Count > 0
                ? new IssueCommandResult(IssueCommandOutcome.Success, created)
                : new IssueCommandResult(IssueCommandOutcome.AllDuplicates, [], "A command of that type is already pending for the targeted device(s).");
        }

        /// Instant best-effort MQTT push at issue time, immediately marked published either way - a swallowed broker failure here is terminal (matches MqttCommandPublisher's own "never blocks the triggering request" contract), not something DeviceOutboxDispatchEvaluator's sweep should retry.
        private async Task PublishAndMarkAsync(Device target, PendingCommand command)
        {
            await mqttPublisher.PublishAsync(target, command);
            await outboxRepo.MarkPublishedAsync(command.IDDeviceCommand, DateTime.UtcNow);
        }

        /// The device's full pending-outbox state for one Config poll: the oldest non-expired actionable item (if any), and whether a ConfigChanged/HardReset signal is separately pending - lazily expires any stale Pending rows first so a stuck expired item never hides a still-valid one of a different type.
        public async Task<PendingItems> GetPendingAsync(int deviceId)
        {
            DateTime utcNow = DateTime.UtcNow;
            IList<DeviceCommand> candidates = await outboxRepo.GetPendingOutboxItemsAsync(deviceId); // oldest first

            if (candidates.Any(c => c.ExpiresAt <= utcNow))
            {
                await outboxRepo.ExpirePendingOutboxItemsAsync(deviceId, utcNow); // one bulk statement, not one write per expired row
            }

            PendingCommand? actionable = null;
            bool configChangePending = false;
            bool hardResetPending = false;
            foreach (var candidate in candidates)
            {
                if (candidate.ExpiresAt <= utcNow)
                {
                    continue;
                }
                if (candidate.ActionType == CommandActionType.ConfigChanged)
                {
                    configChangePending = true;
                    continue;
                }
                if (candidate.ActionType == CommandActionType.HardReset)
                {
                    hardResetPending = true;
                    continue;
                }
                actionable ??= new PendingCommand
                {
                    IDDeviceCommand = candidate.IDDeviceCommand,
                    ActionType = candidate.ActionType,
                    ExpiresAt = candidate.ExpiresAt,
                    Payload = candidate.Payload,
                };
            }
            return new PendingItems(actionable, configChangePending, hardResetPending);
        }

        /// Marks any still-Pending ConfigChanged row(s) for this device Executed - called unconditionally whenever DeviceConfigBuilder.BuildAsync actually sends a full config, since that's true regardless of which of NeedsRefreshAsync's three reasons triggered the send; a no-op when nothing was pending.
        public Task ConsumePendingConfigChangeAsync(int deviceId) =>
            outboxRepo.ConsumePendingByTypeAsync(deviceId, CommandActionType.ConfigChanged, DateTime.UtcNow);

        /// Queues a HardReset outbox item (GlobalAdmin-initiated, DeviceApiController.HardResetRequest) - pushed over MQTT immediately when possible, same instant-delivery treatment as a real command, alongside riding the next Config poll's Reset field / the anonymous HardResetPending fallback.
        public async Task EnqueueHardResetAsync(int deviceId)
        {
            DateTime utcNow = DateTime.UtcNow;
            if (await outboxRepo.AddOutboxItemAsync(deviceId, CommandActionType.HardReset, utcNow, utcNow + HardResetExpiry) is int newCommandId)
            {
                Device? target = await deviceRepo.DeviceGetByIdAsync(deviceId);
                if (target != null)
                {
                    await PublishAndMarkAsync(target, new PendingCommand
                    {
                        IDDeviceCommand = newCommandId,
                        ActionType = CommandActionType.HardReset,
                        ExpiresAt = utcNow + HardResetExpiry,
                    });
                }
            }
        }

        /// Fire-once consume: true only the first time a Pending HardReset row is observed for this device (by whichever path checks first - the normal poll's Reset field, or the anonymous HardResetPending fallback for a device whose apiKey itself is broken) - mirrors the old device.Reset boolean's exact semantics.
        public async Task<bool> ConsumeHardResetIfPendingAsync(int deviceId)
        {
            IList<DeviceCommand> candidates = await outboxRepo.GetPendingOutboxItemsAsync(deviceId);
            bool pending = candidates.Any(c => c.ActionType == CommandActionType.HardReset && c.ExpiresAt > DateTime.UtcNow);
            if (pending)
            {
                await outboxRepo.ConsumePendingByTypeAsync(deviceId, CommandActionType.HardReset, DateTime.UtcNow);
            }
            return pending;
        }

        /// Only a genuinely Pending command can be acknowledged; a command belonging to a different device is treated as not found.
        public async Task AcknowledgeCommandAsync(int commandId, int deviceId)
        {
            DeviceCommand? command = await outboxRepo.GetOutboxItemByIdAsync(commandId);
            if (command?.Status == CommandStatus.Pending && command.DeviceID == deviceId)
            {
                await outboxRepo.SetOutboxItemStatusAsync(commandId, CommandStatus.Acknowledged);
            }
        }

        /// Accepts either Pending or Acknowledged as the prior state - Pending covers Reboot, which has no "after" to ack from; same ownership check as AcknowledgeCommandAsync. Returns the command actually marked executed (null if the ownership/state check failed) so a caller can branch on its ActionType, e.g. DeviceApiController.PushEvent persisting a DetectSensors result.
        public async Task<DeviceCommand?> MarkExecutedAsync(int commandId, int deviceId)
        {
            DeviceCommand? command = await outboxRepo.GetOutboxItemByIdAsync(commandId);
            if (command != null && command.Status is CommandStatus.Pending or CommandStatus.Acknowledged && command.DeviceID == deviceId)
            {
                await outboxRepo.SetOutboxItemStatusAsync(commandId, CommandStatus.Executed, DateTime.UtcNow);
                return command;
            }
            return null;
        }
    }
}
