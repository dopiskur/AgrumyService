using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Server-wide settings facet.
    public interface IServerConfigRepository
    {
        Task<ServerConfig> ServerConfigGetAsync(int idServerConfig);
        Task ServerConfigUpdateAsync(ServerConfig config);

        /// Overwrites the DB row's hysteresis fields from appsettings.json - only called at startup when AgrumySettings.ServerConfigReload is true.
        Task ServerConfigReloadFromAppSettingsAsync(int idServerConfig);

        /// Narrow writer for FirmwareCatalogRefreshEvaluator's last-run timestamp, kept separate from ServerConfigUpdateAsync so a concurrent settings save can't race it.
        Task ServerConfigFirmwareRefreshStateSetAsync(DateTimeOffset checkedAtUtc, int idServerConfig);

        /// Narrow writer for SensorDataArchiveEvaluator's last-run timestamp, same isolation reasoning as ServerConfigFirmwareRefreshStateSetAsync.
        Task ServerConfigArchiveRunStateSetAsync(DateTimeOffset ranAtUtc, int idServerConfig);

        /// Narrow writer for ArkodGeoPackageSyncService's last-run timestamp, same isolation reasoning as ServerConfigFirmwareRefreshStateSetAsync.
        Task ServerConfigArkodSyncStateSetAsync(DateTimeOffset syncedAtUtc, int idServerConfig);

        /// PostgreSQL/TimescaleDB side of sensorData retention (MariaDB's equivalent is SensorDataRetentionBackgroundService's daily purge) - re-applied on every ServerConfig save plus once at startup by ISystemRepository.EnsureSchemaAsync.
        Task ApplyRetentionPolicyAsync(int? retentionDays);
    }
}
