using System.Net.Sockets;
using api.Dal.Interface;
using api.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace api.Diagnostics
{
    /// TCP-connect only, not a real SMTP handshake or test email - #223's TestEmail already covers "does auth + sending actually work"; this just needs to be cheap enough to run on every passive health-card refresh.
    internal sealed class EmailHealthCheck(IServerConfigRepository serverConfigRepo) : IHealthCheck
    {
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            if (string.IsNullOrWhiteSpace(config.EmailHost))
            {
                return HealthCheckResult.Degraded("Email enabled but no SMTP host configured.");
            }

            using var client = new TcpClient();
            try
            {
                using var timeoutCts = new CancellationTokenSource(ConnectTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                await client.ConnectAsync(config.EmailHost, config.EmailPort, linkedCts.Token);
                return HealthCheckResult.Healthy($"TCP connect to {config.EmailHost}:{config.EmailPort} OK.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Could not reach {config.EmailHost}:{config.EmailPort}: {ex.Message}", ex);
            }
        }
    }
}
