using System.Text.Json;
using System.Text.Json.Nodes;
using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Devices;
using Agrumy.Api.Firmware;
using Agrumy.Shared.LoRa;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Agrumy.Api.Controllers.API
{
    /// Agrumy.Gateway's own endpoints - a gateway is a device row like any other (see Agrumy.Shared.Models.Device.IsGateway) that authenticates the same way (DeviceAuth.ApiKeyPolicy, its own apiId/apiKey), just forwarding OTHER devices' traffic through Batch instead of reporting its own sensors.
    [Route("/api/Gateway")]
    public class GatewayApiController(
        IDeviceRepository deviceRepo, IServerConfigRepository serverConfigRepo, ISensorDataRepository sensorDataRepo, IGatewayRepository gatewayRepo,
        IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, CommandQueueService commandQueue,
        FirmwareCatalogService firmwareCatalog, DeviceConfigBuilder configBuilder, ILogger<GatewayApiController> logger)
        : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        /// The caller's own device row, already confirmed to be a gateway - null (with the ActionResult already set) covers every failure mode, so every action below is one guard clause instead of repeating the same checks.
        private async Task<(Device? gateway, ActionResult? error)> GetCallerGatewayAsync()
        {
            string apiId = HttpContext.DeviceApiId()!;
            Device? gateway = await deviceRepo.DeviceGetByApiIdAsync(apiId);
            if (gateway is null)
            {
                return (null, NotFound());
            }
            if (gateway.IsGateway != true && gateway.LoRaGatewayEnabled != true)
            {
                return (null, StatusCode(403, "This device is not registered as a Gateway."));
            }
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            if (!serverConfig.GatewayEnabled)
            {
                return (null, StatusCode(403, "Gateway support is disabled server-wide (Server Settings -> Gateway)."));
            }
            return (gateway, null);
        }

        // Same 1-minute window as the "device-data" IP limiter, ~1/3 of its 60/min ceiling per leaf - generous for a single node's own telemetry cadence, but nowhere near enough for one bad node to starve every other leaf sharing the gateway's IP budget.
        private const int RelayPerLeafPermitLimit = 20;
        private static readonly TimeSpan RelayPerLeafWindow = TimeSpan.FromMinutes(1);

        // Internal, not private, so GatewayApiControllerTests can construct a mocked cache entry directly.
        internal sealed class RelayRateCounter
        {
            public DateTimeOffset WindowStart { get; set; }
            public int Count { get; set; }
        }

        /// True and increments if idDevice (the RESOLVED leaf, not the gateway) is still under its own per-minute relay ceiling - best-effort (read-modify-write, not atomic), acceptable for a noisy-neighbor guard rather than a hard security boundary.
        private async Task<bool> IsLeafWithinRateLimitAsync(int idDevice)
        {
            string key = $"relay-rate:{idDevice}";
            DateTimeOffset now = DateTimeOffset.UtcNow;
            RelayRateCounter? counter = await Cache.GetAsync<RelayRateCounter>(key);
            if (counter is null || now - counter.WindowStart >= RelayPerLeafWindow)
            {
                await Cache.SetAsync(key, new RelayRateCounter { WindowStart = now, Count = 1 }, RelayPerLeafWindow);
                return true;
            }
            if (counter.Count >= RelayPerLeafPermitLimit)
            {
                return false;
            }
            counter.Count++;
            await Cache.SetAsync(key, counter, RelayPerLeafWindow - (now - counter.WindowStart));
            return true;
        }

        // 500 gives generous headroom for either a small LoRa aggregation batch or a larger WiFi-repeater one, while still bounding a malformed/hostile batch's server-side work.
        private const int MaxBatchEntries = 500;

        /// Iterates a gateway's batched entries, running each through the SAME logic its wrapped single-device endpoint (Config/SensorData/Event/Command.Ack) already uses (see the per-type handlers below) - one entry failing never fails the rest of the batch.
        [HttpPost("Batch")]
        [EnableRateLimiting("device-data")]
        [Authorize(Policy = DeviceAuth.ApiKeyPolicy)]
        public async Task<ActionResult<GatewayBatchResponse>> Batch([FromBody] GatewayBatchRequest request)
        {
            var (gateway, error) = await GetCallerGatewayAsync();
            if (error != null)
            {
                return error;
            }

            if (request.Entries.Count > MaxBatchEntries)
            {
                return BadRequest($"Batch too large: {request.Entries.Count} entries, max {MaxBatchEntries}.");
            }

            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            var results = new List<GatewayBatchEntryResult>(request.Entries.Count);
            foreach (GatewayBatchEntry entry in request.Entries)
            {
                results.Add(await RunEntryAsync(entry, gateway!.TenantID));
            }

            return Ok(new GatewayBatchResponse
            {
                Results = results,
                GatewayMode = serverConfig.GatewayMode,
                GatewayWaitWindowSeconds = serverConfig.GatewayWaitWindowSeconds,
            });
        }

        private async Task<GatewayBatchEntryResult> RunEntryAsync(GatewayBatchEntry entry, int? gatewayTenantId)
        {
            Device? device = await deviceRepo.DeviceGetByApiIdAsync(entry.DeviceApiId);
            // Either the device's own real ApiKey (WiFiRepeater profile - the device built this entry itself, forwarded verbatim) or a GatewayDeviceToken minted for this gateway (LoRaGateway/LoRaPrivateProtocol profiles - see EfGatewayRepository.GatewayDeviceMappingsWithSecretsGetAsync).
            bool authorized = device?.ApiId is string apiId && device.ApiKey is string apiKey
                && (DeviceAuth.ConstantTimeEquals(entry.DeviceApiKey, apiKey) || GatewayDeviceToken.Verify(entry.DeviceApiKey, apiId, apiKey));
            if (device is null || !authorized)
            {
                return new GatewayBatchEntryResult { Success = false, StatusCode = 401, Error = "Unknown device or apiKey mismatch." };
            }
            if (device.TenantID != gatewayTenantId)
            {
                return new GatewayBatchEntryResult { Success = false, StatusCode = 403, Error = "Device belongs to a different tenant than this gateway." };
            }

            try
            {
                return entry.Type switch
                {
                    GatewayEntryType.Config => await RunConfigAsync(device, entry.Payload),
                    GatewayEntryType.SensorData => await RunSensorDataAsync(device, entry.Payload),
                    GatewayEntryType.Event => await RunEventAsync(device, entry.Payload),
                    GatewayEntryType.CommandAck => await RunCommandAckAsync(device, entry.Payload),
                    _ => new GatewayBatchEntryResult { Success = false, StatusCode = 400, Error = $"Unknown entry type: {entry.Type}" },
                };
            }
            catch (JsonException ex)
            {
                // A malformed entry must not take the rest of the batch down with it - same reason SensorController.flushBufferedSensorData() drops a poison file instead of wedging.
                return new GatewayBatchEntryResult { Success = false, StatusCode = 400, Error = "Malformed payload: " + ex.Message };
            }
        }

        /// Same steps as DeviceApiController.GetConfig, in the same order - diagnostics upsert before the version check, so a batched device still bumps LastSeenAt even when nothing changed.
        private async Task<GatewayBatchEntryResult> RunConfigAsync(Device device, JsonElement payload)
        {
            DeviceConfigPoll poll = payload.Deserialize<DeviceConfigPoll>() ?? new DeviceConfigPoll();

            await deviceRepo.DeviceDiagnosticUpsertAsync(device.IDDevice!.Value, device.TenantID ?? 0, poll);

            if (await firmwareCatalog.NoteHeartbeatAsync(device, poll.FirmwareVersion, poll.Board))
            {
                device.FirmwareUpdate = false;
                device.FirmwareTargetVersion = null;
                await deviceRepo.EventDevicePushAsync(device.IDDevice.Value, device.TenantID ?? 0, DeviceEventType.FirmwareUpdated, "version=" + poll.FirmwareVersion);
            }

            PendingCommand? pendingCommand = await commandQueue.GetPendingCommandAsync(device.IDDevice.Value);
            if (!await configBuilder.NeedsRefreshAsync(device, poll.ConfigVersion, pendingCommand))
            {
                return new GatewayBatchEntryResult { Success = true, StatusCode = 200 }; // up to date, nothing queued, no heartbeat due - mirrors GetConfig's empty-200
            }

            DeviceConfig config = await configBuilder.BuildAsync(device, pendingCommand, poll.Board);
            await deviceRepo.DeviceMarkConfigSentAsync(device.IDDevice!.Value, DateTime.UtcNow);
            return new GatewayBatchEntryResult { Success = true, StatusCode = 200, Config = config };
        }

        /// Same steps as SensorDataController.Post.
        private async Task<GatewayBatchEntryResult> RunSensorDataAsync(Device device, JsonElement payload)
        {
            List<SensorDataPushReading> readings = payload.Deserialize<List<SensorDataPushReading>>()
                ?? throw new JsonException("SensorData payload must be a JSON array.");

            await sensorDataRepo.SensorDataPushAsync(readings, device.IDDevice!.Value, device.TenantID ?? 0,
                device.DeviceFarmUnitID, device.DeviceFarmUnitZoneID);

            return new GatewayBatchEntryResult { Success = true, StatusCode = 200 };
        }

        /// Same steps as DeviceApiController.PushEvent.
        private async Task<GatewayBatchEntryResult> RunEventAsync(Device device, JsonElement payload)
        {
            DeviceEventPush push = payload.Deserialize<DeviceEventPush>() ?? new DeviceEventPush();
            if (!Enum.TryParse<DeviceEventType>(push.EventType, ignoreCase: true, out var eventType))
            {
                return new GatewayBatchEntryResult { Success = false, StatusCode = 400, Error = $"Unknown eventType: {push.EventType}" };
            }

            await deviceRepo.EventDevicePushAsync(device.IDDevice!.Value, device.TenantID ?? 0, eventType, push.Message);

            if (eventType == DeviceEventType.CommandExecuted && push.CommandId is int commandId)
            {
                DeviceCommand? command = await commandQueue.MarkExecutedAsync(commandId, device.IDDevice!.Value);
                if (command?.ActionType == CommandActionType.DetectSensors)
                {
                    await PersistSensorDetectionResultAsync(device.IDDevice!.Value, push.Message);
                }
            }

            return new GatewayBatchEntryResult { Success = true, StatusCode = 200 };
        }

        /// Same steps as DeviceApiController.PersistSensorDetectionResultAsync - messageJson is device-supplied and unvalidated, so a malformed payload logs and no-ops rather than failing the whole gateway batch.
        private async Task PersistSensorDetectionResultAsync(int deviceId, string? messageJson)
        {
            DeviceSensorDetectionResult? result;
            try
            {
                result = string.IsNullOrWhiteSpace(messageJson) ? null : JsonSerializer.Deserialize<DeviceSensorDetectionResult>(messageJson);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "DetectSensors result for device {DeviceId} was not valid JSON, discarding.", deviceId);
                return;
            }

            if (result is null)
            {
                return;
            }

            result.DetectedAt = DateTimeOffset.UtcNow;
            await deviceRepo.DeviceSensorDetectionResultSetAsync(deviceId, JsonSerializer.Serialize(result), result.DetectedAt);
        }

        /// Same steps as DeviceApiController.AckCommand.
        private async Task<GatewayBatchEntryResult> RunCommandAckAsync(Device device, JsonElement payload)
        {
            CommandAckRequest ack = payload.Deserialize<CommandAckRequest>() ?? new CommandAckRequest();
            await commandQueue.AcknowledgeCommandAsync(ack.CommandId, device.IDDevice!.Value);
            return new GatewayBatchEntryResult { Success = true, StatusCode = 200 };
        }

        /// What the OWNING gateway itself fetches to build its DevEUI-&gt;apiId/apiKey forwarding cache (GatewayProfile.LoRaGateway) - includes ApiKey, unlike the admin list below, since Gateway must reconstruct each mapped device's own request.
        [HttpGet("DeviceMapping")]
        [EnableRateLimiting("device-data")]
        [Authorize(Policy = DeviceAuth.ApiKeyPolicy)]
        public async Task<ActionResult<IList<GatewayDeviceMapping>>> DeviceMappingGetMine()
        {
            var (gateway, error) = await GetCallerGatewayAsync();
            if (error != null)
            {
                return error;
            }
            return Ok(await gatewayRepo.GatewayDeviceMappingsWithSecretsGetAsync(gateway!.IDDevice!.Value));
        }

        /// Roadmap #383 - the WiFi-relay counterpart to LoRaGatewayBridgeController's serial link: one already RF-decoded frame, resolved against the CALLER's own GatewayDeviceMapping (address stored in DevEUI) and dispatched through the same Config/SensorData/Event/CommandAck handlers Batch uses. A small, fixed catalog (a handful of mapped nodes per gateway at most) - no reason for a per-request cache like Gateway's own client-side one.
        [HttpPost("RelayUplink")]
        [EnableRateLimiting("device-data")]
        [Authorize(Policy = DeviceAuth.ApiKeyPolicy)]
        public async Task<ActionResult<GatewayBatchEntryResult>> RelayUplink([FromBody] GatewayRelayUplinkRequest request)
        {
            var (gateway, error) = await GetCallerGatewayAsync();
            if (error != null)
            {
                return error;
            }

            IList<GatewayDeviceMapping> mappings = await gatewayRepo.GatewayDeviceMappingsGetAsync(gateway!.IDDevice!.Value);
            string address = request.SourceAddress.ToString();
            GatewayDeviceMapping? mapping = mappings.FirstOrDefault(m => m.DevEUI == address);
            if (mapping?.IDDevice is not int idDevice)
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 404, Error = $"Node address {request.SourceAddress} is not mapped for this gateway." });
            }
            Device? device = await deviceRepo.DeviceGetByIdAsync(idDevice);
            if (device is null)
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 404, Error = "Mapped device no longer exists." });
            }
            // [EnableRateLimiting("device-data")] above is keyed by caller IP - the GATEWAY's IP, shared by every leaf node relayed through it (roadmap #396(9)). One noisy/faulty leaf must not exhaust that shared budget for its siblings, so each resolved leaf device gets its own separate ceiling on top.
            if (!await IsLeafWithinRateLimitAsync(idDevice))
            {
                return StatusCode(429, $"Node {request.SourceAddress} (device {idDevice}) is relaying too fast - rate limited independently of this gateway's own budget.");
            }
            if (string.IsNullOrEmpty(device.LoRaPrivateKeyHex))
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 401, Error = "Device has no LoRa private-protocol key provisioned." });
            }

            byte[] key;
            byte[] wirePayload;
            try
            {
                key = Convert.FromHexString(device.LoRaPrivateKeyHex);
                wirePayload = Convert.FromBase64String(request.Payload);
            }
            catch (FormatException ex)
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 400, Error = "Malformed payload: " + ex.Message });
            }

            (string? plaintext, ulong counter) = LoRaPrivatePayloadCrypto.Decrypt(key, wirePayload);
            if (plaintext is null)
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 401, Error = "Decryption failed - wrong key, or the payload was corrupted or tampered with." });
            }
            // Atomic check-and-advance - the WHERE clause inside this call IS the replay check, so two concurrent RelayUplink calls for the same device can never both pass (unlike a separate read-then-compare-then-write, which had exactly that race).
            if (!await deviceRepo.DeviceLoRaUplinkCounterSetAsync(idDevice, (long)counter))
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 409, Error = "Replayed or out-of-order uplink." });
            }

            JsonNode? envelope;
            try
            {
                envelope = JsonNode.Parse(plaintext);
            }
            catch (JsonException ex)
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 400, Error = "Malformed payload: " + ex.Message });
            }
            string? entryTypeTag = envelope?["t"]?.GetValue<string>();
            GatewayEntryType? entryType = entryTypeTag switch
            {
                "config" => GatewayEntryType.Config,
                "sensor" => GatewayEntryType.SensorData,
                "event" => GatewayEntryType.Event,
                "ack" => GatewayEntryType.CommandAck,
                _ => null,
            };
            if (entryType is null || envelope is null)
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 400, Error = $"Unrecognized envelope type: {entryTypeTag}" });
            }

            // Same reasoning as LoRaPrivateProtocolUplinkService.ProcessUplinkAsync: SensorData needs an array payload, the firmware nests readings under "d".
            JsonElement payload = entryType == GatewayEntryType.SensorData && envelope["d"] is JsonNode sensorArray
                ? JsonDocument.Parse(sensorArray.ToJsonString()).RootElement
                : JsonDocument.Parse(envelope.ToJsonString()).RootElement;

            try
            {
                GatewayBatchEntryResult result = entryType switch
                {
                    GatewayEntryType.Config => await RunConfigAsync(device, payload),
                    GatewayEntryType.SensorData => await RunSensorDataAsync(device, payload),
                    GatewayEntryType.Event => await RunEventAsync(device, payload),
                    GatewayEntryType.CommandAck => await RunCommandAckAsync(device, payload),
                    _ => new GatewayBatchEntryResult { Success = false, StatusCode = 400, Error = $"Unrecognized envelope type: {entryTypeTag}" },
                };
                return Ok(result);
            }
            catch (JsonException ex)
            {
                return Ok(new GatewayBatchEntryResult { Success = false, StatusCode = 400, Error = "Malformed payload: " + ex.Message });
            }
        }

        // ---- admin (Gateway Devices page) ---------------------------------------------------

        [HttpGet("All")]
        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult<IList<DeviceDto>>> GatewaysGetAll() =>
            Ok((await gatewayRepo.GatewayDevicesGetAllAsync()).Select(d => d.ToDto()).ToList());

        /// Looks the gateway device up and checks the caller may touch it - same shared 404/403 logic every other Device-domain controller uses (ApiControllerBase.EnsureOwnedDeviceEntityAsync).
        private Task<(Device? Gateway, ActionResult? Error)> EnsureOwnedGatewayAsync(int idGatewayDevice, bool forWrite) =>
            EnsureOwnedDeviceEntityAsync(() => deviceRepo.DeviceGetByIdAsync(idGatewayDevice), d => d.TenantID, "Gateway", forWrite);

        [HttpGet("DeviceMapping/All")]
        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult<IList<GatewayDeviceMapping>>> DeviceMappingGetAll(int idGatewayDevice)
        {
            var (_, error) = await EnsureOwnedGatewayAsync(idGatewayDevice, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await gatewayRepo.GatewayDeviceMappingsGetAsync(idGatewayDevice));
        }

        [HttpPost("DeviceMapping")]
        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult<bool>> DeviceMappingAdd([FromBody] GatewayDeviceMapping value)
        {
            if (string.IsNullOrWhiteSpace(value.DevEUI) || value.IDGatewayDevice is not int idGateway || value.IDDevice is not int idDevice)
            {
                return BadRequest("IDGatewayDevice, DevEUI and IDDevice are required.");
            }
            var (gateway, error) = await EnsureOwnedGatewayAsync(idGateway, forWrite: true);
            if (error != null)
            {
                return error;
            }
            bool added = await gatewayRepo.GatewayDeviceMappingAddAsync(idGateway, value.DevEUI.Trim().ToUpperInvariant(), idDevice, gateway!.TenantID);
            return added ? Ok(true) : Conflict("That DevEUI is already mapped for this gateway, or IDDevice does not exist or belongs to a different tenant than the gateway.");
        }

        [HttpDelete("DeviceMapping")]
        [Authorize(Roles = RoleNames.DeviceManagers)]
        public async Task<ActionResult<bool>> DeviceMappingDelete(int idGatewayDeviceMapping, int idGatewayDevice)
        {
            var (_, error) = await EnsureOwnedGatewayAsync(idGatewayDevice, forWrite: true);
            if (error != null)
            {
                return error;
            }
            return Ok(await gatewayRepo.GatewayDeviceMappingDeleteAsync(idGatewayDeviceMapping, idGatewayDevice));
        }
    }
}
