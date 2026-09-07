using MQTTnet;
using MQTTnet.Client;

namespace api.Commands
{
    public interface IMqttConnectionManager
    {
        Task PublishAsync(string brokerHost, int brokerPort, string? username, string? password, MqttApplicationMessage message, CancellationToken ct = default);
    }

    /// One persistent MQTT connection reused across every publish, instead of MqttCommandPublisher's old per-call connect/disconnect - a broker round trip (TCP, plus TLS handshake when using it) costs far more than the publish itself, and command pushes can be frequent under real load. Singleton, so PublishAsync serializes connect/reconnect through a semaphore - concurrent callers must never race to open multiple sockets on the same client.
    public sealed class MqttConnectionManager(IMqttClient client) : IMqttConnectionManager, IAsyncDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private string? connectedFingerprint;

        public async Task PublishAsync(string brokerHost, int brokerPort, string? username, string? password, MqttApplicationMessage message, CancellationToken ct = default)
        {
            // Not the message itself - just enough to tell "same broker as last time" from "admin changed the broker settings, must reconnect".
            string fingerprint = $"{brokerHost}:{brokerPort}:{username}";

            await gate.WaitAsync(ct);
            try
            {
                bool wasConnected = client.IsConnected;
                if (!wasConnected || connectedFingerprint != fingerprint)
                {
                    if (wasConnected)
                    {
                        await client.DisconnectAsync(cancellationToken: ct);
                    }

                    var optionsBuilder = new MqttClientOptionsBuilder()
                        .WithTcpServer(brokerHost, brokerPort)
                        .WithCleanSession();
                    // Same convention as AgrumyFirmware's MqttController - the standard MQTT-over-TLS port, no separate ServerConfig toggle needed.
                    if (brokerPort == 8883)
                    {
                        optionsBuilder = optionsBuilder.WithTlsOptions(o => o.UseTls());
                    }
                    if (!string.IsNullOrEmpty(username))
                    {
                        optionsBuilder = optionsBuilder.WithCredentials(username, password);
                    }

                    await client.ConnectAsync(optionsBuilder.Build(), ct);
                    connectedFingerprint = fingerprint;
                }

                await client.PublishAsync(message, ct);
            }
            finally
            {
                gate.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync();
            }
            client.Dispose();
            gate.Dispose();
        }
    }
}
