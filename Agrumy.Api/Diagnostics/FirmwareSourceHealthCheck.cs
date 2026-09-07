using api.Dal.Interface;
using api.Firmware;
using api.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace api.Diagnostics
{
    /// Only meaningful for GitHub/Custom sources - Local never reaches the network (FirmwareCatalogService), so ServerHealthService skips this check entirely when FirmwareSource is Local. Reuses IFirmwareFetcher so the probe goes through the same SsrfGuard as a real catalog refresh.
    internal sealed class FirmwareSourceHealthCheck(IServerConfigRepository serverConfigRepo, IFirmwareFetcher firmwareFetcher) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            try
            {
                switch (config.FirmwareSource)
                {
                    case FirmwareSource.GitHub:
                        if (string.IsNullOrWhiteSpace(config.FirmwareGitHubRepository))
                        {
                            return HealthCheckResult.Degraded("GitHub firmware source has no repository configured.");
                        }
                        await firmwareFetcher.GetStringAsync($"https://api.github.com/repos/{config.FirmwareGitHubRepository}", gitHubApi: true, cancellationToken);
                        return HealthCheckResult.Healthy($"Reached GitHub repository {config.FirmwareGitHubRepository}.");

                    case FirmwareSource.Custom:
                        if (string.IsNullOrWhiteSpace(config.FirmwareCustomRepositoryUrl))
                        {
                            return HealthCheckResult.Degraded("Custom firmware source has no manifest URL configured.");
                        }
                        await firmwareFetcher.GetStringAsync(config.FirmwareCustomRepositoryUrl, gitHubApi: false, cancellationToken);
                        return HealthCheckResult.Healthy($"Reached {config.FirmwareCustomRepositoryUrl}.");

                    default:
                        return HealthCheckResult.Healthy("Local firmware source - no external dependency.");
                }
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Firmware source unreachable: {ex.Message}", ex);
            }
        }
    }
}
