using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IServerConfigRepository members - forwarded to the standalone EfServerConfigRepository so RelationalIntegrationTests can drive many facets through one object. Defaults preserved on idServerConfig since some internal callers (Devices.cs, DeviceFarmUnits.cs, Devices.Diagnostics.cs) rely on them.
    internal partial class AllFacetsRepository
    {
        public Task<ServerConfig> ServerConfigGetAsync(int idServerConfig = 1) => serverConfigRepository.ServerConfigGetAsync(idServerConfig);

        public Task ServerConfigUpdateAsync(ServerConfig config) => serverConfigRepository.ServerConfigUpdateAsync(config);

        public Task ServerConfigReloadFromAppSettingsAsync(int idServerConfig = 1) => serverConfigRepository.ServerConfigReloadFromAppSettingsAsync(idServerConfig);

        public Task ServerConfigWeatherStateSetAsync(bool rainPredicted, DateTimeOffset checkedAtUtc, int idServerConfig = 1) =>
            serverConfigRepository.ServerConfigWeatherStateSetAsync(rainPredicted, checkedAtUtc, idServerConfig);

        public Task ServerConfigFirmwareRefreshStateSetAsync(DateTimeOffset checkedAtUtc, int idServerConfig = 1) =>
            serverConfigRepository.ServerConfigFirmwareRefreshStateSetAsync(checkedAtUtc, idServerConfig);

        public Task ServerConfigArchiveRunStateSetAsync(DateTimeOffset ranAtUtc, int idServerConfig = 1) =>
            serverConfigRepository.ServerConfigArchiveRunStateSetAsync(ranAtUtc, idServerConfig);

        public Task ApplyRetentionPolicyAsync(int? retentionDays) => serverConfigRepository.ApplyRetentionPolicyAsync(retentionDays);
    }
}
