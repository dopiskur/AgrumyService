using System.IO.Ports;
using System.Text.Json;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agrumy.Gateway.LoRaPrivate
{
    /// GatewayProfile.LoRaPrivateProtocol - reads AgrumySerialFrame uplinks from a locally-attached
    /// ESP32+SX126x radio-frontend board (RadioLib raw PHY, not LoRaWAN/ChirpStack) and forwards each,
    /// still encrypted, through /api/Gateway/RelayUplink (same path LoRaGatewayRelayController.cpp's
    /// WiFi-direct profile uses - this gateway never holds the decryption key, see ProcessUplinkAsync),
    /// writing the result back as a downlink frame.
    public sealed partial class LoRaPrivateProtocolUplinkService(
        AgrumyServiceClient client, IOptions<GatewayOptions> options, ILogger<LoRaPrivateProtocolUplinkService> logger)
        : BackgroundService
    {
        private readonly LoRaPrivateProtocolOptions cfg = options.Value.LoRaPrivateProtocol;
        private Dictionary<ushort, GatewayDeviceMapping> mappingByAddress = new();
        private SerialPort? port;

        [LoggerMessage(Level = LogLevel.Information, Message = "Opened radio-frontend serial port {Port} at {BaudRate} baud.")]
        private static partial void LogPortOpened(ILogger logger, string port, int baudRate);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Radio-frontend serial port open failed, retrying in 30s.")]
        private static partial void LogPortOpenFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Refreshed node-address mapping: {Count} entries.")]
        private static partial void LogMappingRefreshed(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not refresh node-address mapping - keeping the previous cache.")]
        private static partial void LogMappingRefreshFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to process a LoRa private-protocol uplink - skipped.")]
        private static partial void LogUplinkFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Uplink from unmapped node address {Address} - dropped.")]
        private static partial void LogUnmappedAddress(ILogger logger, ushort address);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RefreshMappingAsync(stoppingToken);
            _ = MappingRefreshLoopAsync(stoppingToken);

            var readBuffer = new List<byte>(1024);
            var chunk = new byte[256];

            while (!stoppingToken.IsCancellationRequested)
            {
                if (port is null || !port.IsOpen)
                {
                    if (!TryOpenPort())
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                        continue;
                    }
                }

                try
                {
                    int read = await port!.BaseStream.ReadAsync(chunk, stoppingToken);
                    if (read <= 0)
                    {
                        continue;
                    }
                    readBuffer.AddRange(chunk.AsSpan(0, read).ToArray());
                    ConsumeBuffer(readBuffer);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Board unplugged/power-cycled - drop the port and let the top of the loop reopen it.
                    LogPortOpenFailed(logger, ex);
                    port?.Dispose();
                    port = null;
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }

        private bool TryOpenPort()
        {
            try
            {
                port = new SerialPort(cfg.SerialPort, cfg.BaudRate) { ReadTimeout = 5000, WriteTimeout = 5000 };
                port.Open();
                LogPortOpened(logger, cfg.SerialPort, cfg.BaudRate);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                LogPortOpenFailed(logger, ex);
                return false;
            }
        }

        /// Decodes as many complete frames as `readBuffer` currently holds, dispatching each and trimming the buffer as it goes - a partial trailing frame is left in place for the next read.
        private void ConsumeBuffer(List<byte> readBuffer)
        {
            while (readBuffer.Count > 0)
            {
                int consumed = AgrumySerialFrame.TryDecodeUplink(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(readBuffer), out var decoded);
                if (consumed == 0)
                {
                    break; // wait for more bytes
                }
                readBuffer.RemoveRange(0, consumed);
                if (decoded is { } uplink)
                {
                    _ = HandleUplinkAsync(uplink);
                }
            }
        }

        private async Task MappingRefreshLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, cfg.MappingRefreshSeconds)), ct);
                await RefreshMappingAsync(ct);
            }
        }

        private async Task RefreshMappingAsync(CancellationToken ct)
        {
            try
            {
                IList<GatewayDeviceMapping> mappings = await client.GetDeviceMappingAsync(ct);
                mappingByAddress = mappings
                    .Where(m => ushort.TryParse(m.DevEUI, out _))
                    .ToDictionary(m => ushort.Parse(m.DevEUI!));
                LogMappingRefreshed(logger, mappingByAddress.Count);
            }
            catch (Exception ex)
            {
                LogMappingRefreshFailed(logger, ex);
            }
        }

        private async Task HandleUplinkAsync(AgrumySerialFrame.DecodedUplink uplink)
        {
            try
            {
                await ProcessUplinkAsync(uplink);
            }
            catch (Exception ex)
            {
                // One malformed/unmapped uplink must never take the read loop down - same reasoning as ChirpStackUplinkService.OnUplinkAsync.
                LogUplinkFailed(logger, ex);
            }
        }

        /// Roadmap #395 finding 3 - uplink.Payload is now AES-256-GCM ciphertext (Logic/CommandReplayLogic-style counter+tag, see Agrumy.Shared.LoRa.LoRaPrivatePayloadCrypto), not plaintext JSON. This gateway never holds the decryption key - it forwards the still-encrypted bytes to RelayUplink exactly as LoRaGatewayRelayController.cpp's WiFi-direct path already does, and AgrumyService is the only place that ever decrypts. The local mappingByAddress check stays as a cheap pre-filter (skip a network round trip for noise on an unmapped address); it no longer needs DeviceApiKey for anything, only confirms the address is worth forwarding at all.
        private async Task ProcessUplinkAsync(AgrumySerialFrame.DecodedUplink uplink)
        {
            if (!mappingByAddress.ContainsKey(uplink.SourceAddress))
            {
                LogUnmappedAddress(logger, uplink.SourceAddress);
                return;
            }

            var request = new GatewayRelayUplinkRequest
            {
                SourceAddress = uplink.SourceAddress,
                Payload = Convert.ToBase64String(uplink.Payload),
            };
            GatewayBatchEntryResult result = await client.RelayUplinkAsync(request, CancellationToken.None);

            await SendDownlinkAsync(uplink.SourceAddress, result);
        }

        /// No Class A RX-window timing to respect here (that's a LoRaWAN concept) - the radio-frontend queues this for its next transmit opportunity after the node's own receive window, per LoRaPrivateController's fixed post-uplink listen slot.
        private async Task SendDownlinkAsync(ushort nodeAddress, GatewayBatchEntryResult? result)
        {
            if (port is not { IsOpen: true })
            {
                return;
            }

            // "Empty" success (Config with nothing new) skips the downlink, same as ChirpStackUplinkService.
            if (result is not ({ Success: true, Config: not null } or { Success: false }))
            {
                return;
            }

            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                ok = result?.Success ?? false,
            });
            byte[] frame = AgrumySerialFrame.EncodeDownlink(nodeAddress, payload);
            await port.BaseStream.WriteAsync(frame, CancellationToken.None);
        }

        public override void Dispose()
        {
            port?.Dispose();
            base.Dispose();
        }
    }
}
