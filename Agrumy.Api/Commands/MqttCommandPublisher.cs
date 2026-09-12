using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using MQTTnet;

namespace Agrumy.Api.Commands
{
    public interface IMqttCommandPublisher
    {
        Task PublishAsync(Device device, PendingCommand command, CancellationToken ct = default);
    }

    /// Pure topic convention matching AgrumyFirmware's MqttController subscribe topic exactly, kept separate from MqttCommandPublisher so the wire contract is testable without a broker.
    public static class MqttCommandTopic
    {
        public static string ForDevice(int tenantId, int deviceId) => $"agrumy/{tenantId}/{deviceId}/command";
    }

    /// Best-effort instant command delivery over MQTT alongside the HTTP/JWT poll cycle - never the only way a command reaches a device, since DeviceOutboxService.GetPendingAsync's next poll response always carries it too; a no-op when MqttTransportEnabled is off/unconfigured or the device's organization quota disallows MQTT, and any broker/network failure is swallowed so it can never fail the triggering request.
    public sealed class MqttCommandPublisher(IServerConfigRepository serverConfigRepo, IMqttConnectionManager connectionManager, Agrumy.Api.Quota.TenantQuotaEnforcer quotaEnforcer, ILogger<MqttCommandPublisher> logger) : IMqttCommandPublisher
    {
        public async Task PublishAsync(Device device, PendingCommand command, CancellationToken ct = default)
        {
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            if (!serverConfig.MqttTransportEnabled || string.IsNullOrWhiteSpace(serverConfig.MqttBrokerHost) || device.IDDevice is not int deviceId)
            {
                return;
            }
            if (!await quotaEnforcer.IsMqttAllowedAsync(device.TenantID))
            {
                return;
            }

            string topic = MqttCommandTopic.ForDevice(device.TenantID ?? 0, deviceId);
            // ConditionConfigJson.Options camelCases properties and leaves ActionType as its int, matching the shape the HTTP config-poll response already sends this same type as.
            byte[] commandBytes = JsonSerializer.SerializeToUtf8Bytes(command, ConditionConfigJson.Options);

            // Anyone with broker credentials could otherwise command any device on any organization (MqttUsername/Password are ServerConfig-wide, not per-device) - signing with the TARGET device's own ApiKey means forging a command needs that specific device's credential, not just broker access.
            JsonNode commandNode = JsonNode.Parse(commandBytes)!;
            string canonical = CanonicalString(commandNode);
            commandNode["sig"] = ComputeSignature(canonical, device.ApiKey ?? "");
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(commandNode);

            try
            {
                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .Build();
                await connectionManager.PublishAsync(serverConfig.MqttBrokerHost, serverConfig.MqttBrokerPort, serverConfig.MqttUsername, serverConfig.MqttPassword, message, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MQTT instant command push failed for device {DeviceId} - falling back to normal poll delivery.", deviceId);
            }
        }

        // Plain '|'-joined fields, not a JSON canonicalization - firmware parses idDeviceCommand/actionType as ints and expiresAt/payload as opaque strings it never reformats, so reconstructing this same string from its own parsed fields is guaranteed byte-identical without needing ArduinoJson and System.Text.Json to agree on any serialization convention.
        internal static string CanonicalString(JsonNode command) =>
            string.Join('|',
                command["idDeviceCommand"]!.GetValue<int>(),
                command["actionType"]!.GetValue<int>(),
                command["expiresAt"]?.GetValue<string>() ?? "",
                command["payload"]?.GetValue<string>() ?? "");

        internal static string ComputeSignature(string canonical, string apiKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }
    }
}
