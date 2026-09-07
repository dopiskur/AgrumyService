using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Agrumy.Api.Diagnostics
{
    /// Actively (re)connects via IMqttConnectionManager.TestConnectionAsync - MQTT otherwise only connects lazily on the first real command push, so a passive health card would stay blank until then.
    internal sealed class MqttHealthCheck(IServerConfigRepository serverConfigRepo, IMqttConnectionManager mqttConnectionManager) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            if (string.IsNullOrWhiteSpace(config.MqttBrokerHost))
            {
                return HealthCheckResult.Degraded("MQTT enabled but no broker host configured.");
            }

            try
            {
                bool connected = await mqttConnectionManager.TestConnectionAsync(
                    config.MqttBrokerHost, config.MqttBrokerPort, config.MqttUsername, config.MqttPassword, cancellationToken);
                return connected
                    ? HealthCheckResult.Healthy($"Connected to {config.MqttBrokerHost}:{config.MqttBrokerPort}.")
                    : HealthCheckResult.Unhealthy($"Could not connect to {config.MqttBrokerHost}:{config.MqttBrokerPort}.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"MQTT broker connection failed: {ex.Message}", ex);
            }
        }
    }
}
