using Agrumy.Dal;
using Agrumy.Shared;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Agrumy.Api.Dal
{
    /// IServerConfigRepository - a leaf facet, no dependency on any other domain (ICache is infra, same as AgrumyDbContext/IOptions/ILogger, not a domain dependency - EfDeviceRepository's own Fleet cache is the same shape). Widely read-from by other domains, but that's calls INTO this class, not out of it.
    internal sealed class EfServerConfigRepository(AgrumyDbContext db, IOptions<AgrumySettings> settingsOptions, ILogger<EfServerConfigRepository> logger, ISecretProtector secretProtector, ICache cache) : IServerConfigRepository
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        /// Read on essentially every request across the whole app - cached same shape as EfDeviceRepository.DeviceFleetGetAsync, with every write path below explicitly invalidating it rather than relying on the TTL alone.
        public async Task<ServerConfig> ServerConfigGetAsync(int idServerConfig = 1)
        {
            string cacheKey = CacheKeys.ServerConfig(idServerConfig);
            ServerConfig? cached = await cache.GetAsync<ServerConfig>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var row = await db.ServerConfigs.AsNoTracking()
                .FirstOrDefaultAsync(s => s.IDServerConfig == idServerConfig);

            if (row != null)
            {
                ServerConfig dto = ToDto(row);
                await cache.SetAsync(cacheKey, dto, CacheKeys.ServerConfigTtl);
                return dto;
            }

            // No row: generate one, seeding hysteresis fields from appsettings.json so a fresh install starts with sane defaults.
            var generated = new ServerConfigRow
            {
                IDServerConfig = idServerConfig,
                PortHTTP = 80,
                PortHTTPS = 443,
                WaterLevelHysteresis = settings.HysteresisWaterLevel,
                TemperatureHysteresis = settings.HysteresisTemperature,
                HumidityHysteresis = settings.HysteresisHumidity,
                LightHysteresis = settings.HysteresisLight,
                BatteryLowThreshold = settings.BatteryLowThreshold,
                BatteryLowHysteresis = settings.BatteryLowHysteresis,
                TankRefillThreshold = settings.TankRefillThreshold,
                TankRefillHysteresis = settings.TankRefillHysteresis,
                WaterPumpMaxRunSeconds = settings.WaterPumpMaxRunSeconds,
                WaterPumpCooldownSeconds = settings.WaterPumpCooldownSeconds,
                EventDedupeMinutes = settings.EventDedupeMinutes,
                ActivationResendCooldownMinutes = settings.ActivationResendCooldownMinutes,
                MaxRulesPerZone = settings.MaxRulesPerZone,
                AllowSelfServiceTenantCreation = settings.AllowSelfServiceTenantCreation,
                TenantManagementEnabled = settings.TenantManagementEnabled,
                FirmwareSource = (int)FirmwareSource.GitHub,
                FirmwareGitHubRepository = settings.FirmwareGitHubRepository,
                FirmwareRefreshIntervalHours = settings.FirmwareRefreshIntervalHours,
                SensorDataRetentionDays = settings.SensorDataRetentionDays,
                RecycleBinRetentionDays = 30,
                SatelliteRasterRetentionDays = 30,
                WeatherPollIntervalMinutes = settings.WeatherPollIntervalMinutes,
                WeatherRainSkipThreshold = settings.WeatherRainSkipThreshold,
                FrostLookaheadHours = settings.FrostLookaheadHours,
                FrostTempThresholdC = settings.FrostTempThresholdC,
                FrostCloudinessMaxPercent = settings.FrostCloudinessMaxPercent,
                FrostWindMaxMetersPerSecond = settings.FrostWindMaxMetersPerSecond,
                GatewayWaitWindowSeconds = 30,
                ProblemEventAlertsEnabled = true,
                ProblemEventExpiryHours = 24,
                PasswordMinLength = 8,
                ConfigHeartbeatHours = 1,
                MqttBrokerPort = 1883,
                EmailPort = 587,
                EmailUseStartTls = true,
                EmailFromName = "Agrumy",
                DevicePinValidMinutes = 60,
                ArchivePort = 3306,
            };
            db.ServerConfigs.Add(generated);
            await db.SaveChangesAsync();
            ServerConfig generatedDto = ToDto(generated);
            await cache.SetAsync(cacheKey, generatedDto, CacheKeys.ServerConfigTtl);
            return generatedDto;
        }

        /// Invalidates the cache entry ServerConfigGetAsync writes - called from every write path below, not just here, so a stale settings row is never served after any of them.
        private Task InvalidateCacheAsync(int idServerConfig) => cache.RemoveAsync(CacheKeys.ServerConfig(idServerConfig));

        public async Task ServerConfigUpdateAsync(ServerConfig config)
        {
            var row = await db.ServerConfigs.FirstOrDefaultAsync(s => s.IDServerConfig == config.IDServerConfig);
            if (row == null)
            {
                return;
            }

            row.WaterLevelHysteresis = config.WaterLevelHysteresis;
            row.TemperatureHysteresis = config.TemperatureHysteresis;
            row.HumidityHysteresis = config.HumidityHysteresis;
            row.LightHysteresis = config.LightHysteresis;
            row.BatteryLowThreshold = config.BatteryLowThreshold;
            row.BatteryLowHysteresis = config.BatteryLowHysteresis;
            row.TankRefillThreshold = config.TankRefillThreshold;
            row.TankRefillHysteresis = config.TankRefillHysteresis;
            row.WaterPumpMaxRunSeconds = config.WaterPumpMaxRunSeconds;
            row.WaterPumpCooldownSeconds = config.WaterPumpCooldownSeconds;
            row.EventDedupeMinutes = config.EventDedupeMinutes;
            row.ActivationResendCooldownMinutes = config.ActivationResendCooldownMinutes;
            row.MaxRulesPerZone = config.MaxRulesPerZone;
            row.AllowSelfServiceTenantCreation = config.AllowSelfServiceTenantCreation;
            row.TenantManagementEnabled = config.TenantManagementEnabled;
            row.FirmwareSource = (int)config.FirmwareSource;
            row.FirmwareGitHubRepository = config.FirmwareGitHubRepository;
            row.FirmwareCustomRepositoryUrl = config.FirmwareCustomRepositoryUrl;
            row.FirmwareRefreshIntervalHours = config.FirmwareRefreshIntervalHours;
            // FirmwareLastRefreshedAtUtc deliberately NOT written here - see ServerConfigFirmwareRefreshStateSetAsync below.
            row.SensorDataRetentionDays = config.SensorDataRetentionDays;
            row.RecycleBinRetentionDays = config.RecycleBinRetentionDays;
            row.SatelliteRasterRetentionDays = config.SatelliteRasterRetentionDays;
            row.PurgeOrphanedSensorDataScheduleEnabled = config.PurgeOrphanedSensorDataScheduleEnabled;
            row.WeatherLocationLat = config.WeatherLocationLat;
            row.WeatherLocationLon = config.WeatherLocationLon;
            row.WeatherPollIntervalMinutes = config.WeatherPollIntervalMinutes;
            row.WeatherRainSkipThreshold = config.WeatherRainSkipThreshold;
            row.FrostLookaheadHours = config.FrostLookaheadHours;
            row.FrostTempThresholdC = config.FrostTempThresholdC;
            row.FrostCloudinessMaxPercent = config.FrostCloudinessMaxPercent;
            row.FrostWindMaxMetersPerSecond = config.FrostWindMaxMetersPerSecond;
            row.ODataEnabled = config.ODataEnabled;
            row.ArkodGeoPackageSyncEnabled = config.ArkodGeoPackageSyncEnabled;
            // ArkodGeoPackageSyncedAtUtc deliberately NOT written here - ArkodGeoPackageSyncService owns it, same reasoning as FirmwareLastRefreshedAtUtc.
            row.GatewayEnabled = config.GatewayEnabled;
            row.GatewayMode = (int)config.GatewayMode;
            row.GatewayWaitWindowSeconds = config.GatewayWaitWindowSeconds;
            row.ProblemEventAlertsEnabled = config.ProblemEventAlertsEnabled;
            row.ProblemEventExpiryHours = config.ProblemEventExpiryHours;
            row.PasswordMinLength = config.PasswordMinLength;
            row.PasswordRequireComplexity = config.PasswordRequireComplexity;
            row.ConfigHeartbeatHours = config.ConfigHeartbeatHours;
            row.MqttTransportEnabled = config.MqttTransportEnabled;
            row.MqttBrokerHost = config.MqttBrokerHost;
            row.MqttBrokerPort = config.MqttBrokerPort;
            row.MqttUsername = config.MqttUsername;
            // Blank means "leave the stored password alone" - the edit form never gets the real value back to resubmit, so blank can only mean "unchanged", never "clear it" (clearing means disabling the toggle instead).
            if (!string.IsNullOrEmpty(config.MqttPassword))
            {
                row.MqttPassword = secretProtector.Protect(config.MqttPassword);
            }
            row.EmailEnabled = config.EmailEnabled;
            row.EmailHost = config.EmailHost;
            row.EmailPort = config.EmailPort;
            row.EmailUseStartTls = config.EmailUseStartTls;
            row.EmailUsername = config.EmailUsername;
            row.EmailFromAddress = config.EmailFromAddress;
            row.EmailFromName = config.EmailFromName;
            row.DevicePinValidMinutes = config.DevicePinValidMinutes;
            // Same "blank keeps existing" handling as MqttPassword above.
            if (!string.IsNullOrEmpty(config.EmailPassword))
            {
                row.EmailPassword = secretProtector.Protect(config.EmailPassword);
            }
            row.WebhookEnabled = config.WebhookEnabled;
            row.WebhookUrl = config.WebhookUrl;
            // Same "blank keeps existing" handling as MqttPassword/EmailPassword above.
            if (!string.IsNullOrEmpty(config.WebhookSecret))
            {
                row.WebhookSecret = secretProtector.Protect(config.WebhookSecret);
            }
            row.ArchiveEnabled = config.ArchiveEnabled;
            row.ArchiveCutoffMode = (int)config.ArchiveCutoffMode;
            row.ArchiveCustomCutoffDate = config.ArchiveCustomCutoffDate;
            row.ArchiveCustomRollingDays = config.ArchiveCustomRollingDays;
            row.ArchiveHost = config.ArchiveHost;
            row.ArchivePort = config.ArchivePort;
            row.ArchiveDatabaseName = config.ArchiveDatabaseName;
            row.ArchiveUsername = config.ArchiveUsername;
            // Same "blank keeps existing" handling as MqttPassword/EmailPassword above - ServerConfigApiController.Update only reaches here after TestArchiveConnection has already succeeded with whatever password (new or, if blank, the existing one) it was given.
            if (!string.IsNullOrEmpty(config.ArchivePassword))
            {
                row.ArchivePassword = secretProtector.Protect(config.ArchivePassword);
            }
            await db.SaveChangesAsync();
            await InvalidateCacheAsync(config.IDServerConfig ?? 1);

            // Re-applied on every save so Postgres/TimescaleDB retention updates immediately - a no-op on MariaDB/MySQL, which reads this row fresh on its own daily tick.
            await ApplyRetentionPolicyAsync(config.SensorDataRetentionDays);
        }

        /// Forces hysteresis fields back to appsettings.json's ServerConfig:Hysteresis values (creating the row if missing) - only called at startup when AgrumySettings.ServerConfigReload is true.
        public async Task ServerConfigReloadFromAppSettingsAsync(int idServerConfig = 1)
        {
            var row = await db.ServerConfigs.FirstOrDefaultAsync(s => s.IDServerConfig == idServerConfig);
            if (row == null)
            {
                row = new ServerConfigRow
                {
                    IDServerConfig = idServerConfig,
                    PortHTTP = 80,
                    PortHTTPS = 443,
                };
                db.ServerConfigs.Add(row);
            }

            row.WaterLevelHysteresis = settings.HysteresisWaterLevel;
            row.TemperatureHysteresis = settings.HysteresisTemperature;
            row.HumidityHysteresis = settings.HysteresisHumidity;
            row.LightHysteresis = settings.HysteresisLight;
            row.BatteryLowThreshold = settings.BatteryLowThreshold;
            row.BatteryLowHysteresis = settings.BatteryLowHysteresis;
            row.TankRefillThreshold = settings.TankRefillThreshold;
            row.TankRefillHysteresis = settings.TankRefillHysteresis;
            row.WaterPumpMaxRunSeconds = settings.WaterPumpMaxRunSeconds;
            row.WaterPumpCooldownSeconds = settings.WaterPumpCooldownSeconds;
            row.EventDedupeMinutes = settings.EventDedupeMinutes;
            row.ActivationResendCooldownMinutes = settings.ActivationResendCooldownMinutes;
            row.MaxRulesPerZone = settings.MaxRulesPerZone;
            row.AllowSelfServiceTenantCreation = settings.AllowSelfServiceTenantCreation;
            row.TenantManagementEnabled = settings.TenantManagementEnabled;
            row.SensorDataRetentionDays = settings.SensorDataRetentionDays;
            row.WeatherPollIntervalMinutes = settings.WeatherPollIntervalMinutes;
            row.WeatherRainSkipThreshold = settings.WeatherRainSkipThreshold;
            row.FrostLookaheadHours = settings.FrostLookaheadHours;
            row.FrostTempThresholdC = settings.FrostTempThresholdC;
            row.FrostCloudinessMaxPercent = settings.FrostCloudinessMaxPercent;
            row.FrostWindMaxMetersPerSecond = settings.FrostWindMaxMetersPerSecond;
            row.FirmwareRefreshIntervalHours = settings.FirmwareRefreshIntervalHours;
            await db.SaveChangesAsync();
            await InvalidateCacheAsync(idServerConfig);
            await ApplyRetentionPolicyAsync(settings.SensorDataRetentionDays);
        }

        /// The only writer of FirmwareLastRefreshedAtUtc, called exclusively by FirmwareCatalogRefreshEvaluator - narrower than ServerConfigUpdateAsync so the admin form can't race a fresher reading back to stale.
        public async Task ServerConfigFirmwareRefreshStateSetAsync(DateTimeOffset checkedAtUtc, int idServerConfig = 1)
        {
            var row = await db.ServerConfigs.FirstOrDefaultAsync(s => s.IDServerConfig == idServerConfig);
            if (row == null)
            {
                return;
            }
            row.FirmwareLastRefreshedAtUtc = checkedAtUtc;
            await db.SaveChangesAsync();
            await InvalidateCacheAsync(idServerConfig);
        }

        /// The only writer of ArchiveLastRunAtUtc, called exclusively by SensorDataArchiveEvaluator - same isolation reasoning as ServerConfigFirmwareRefreshStateSetAsync.
        public async Task ServerConfigArchiveRunStateSetAsync(DateTimeOffset ranAtUtc, int idServerConfig = 1)
        {
            var row = await db.ServerConfigs.FirstOrDefaultAsync(s => s.IDServerConfig == idServerConfig);
            if (row == null)
            {
                return;
            }
            row.ArchiveLastRunAtUtc = ranAtUtc;
            await db.SaveChangesAsync();
            await InvalidateCacheAsync(idServerConfig);
        }

        /// The only writer of ArkodGeoPackageSyncedAtUtc, called exclusively by ArkodGeoPackageSyncService - same isolation reasoning as ServerConfigFirmwareRefreshStateSetAsync.
        public async Task ServerConfigArkodSyncStateSetAsync(DateTimeOffset syncedAtUtc, int idServerConfig = 1)
        {
            var row = await db.ServerConfigs.FirstOrDefaultAsync(s => s.IDServerConfig == idServerConfig);
            if (row == null)
            {
                return;
            }
            row.ArkodGeoPackageSyncedAtUtc = syncedAtUtc;
            await db.SaveChangesAsync();
            await InvalidateCacheAsync(idServerConfig);
        }

        /// add_retention_policy's interval can only change by remove-then-add, so this runs unconditionally on every save; also called once at startup by ISystemRepository.EnsureSchemaAsync via EnsureTimescaleHypertableAsync.
        public async Task ApplyRetentionPolicyAsync(int? retentionDays)
        {
            if (!db.Database.IsNpgsql())
            {
                return;
            }

            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    """SELECT remove_retention_policy('"dataSensor"'::regclass, if_exists => true);""");

                if (retentionDays is > 0)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""SELECT add_retention_policy('"dataSensor"'::regclass, INTERVAL '1 day' * {retentionDays.Value}, if_not_exists => true);""");
                }
            }
            catch (PostgresException ex)
            {
                logger.LogWarning(ex,
                    "Could not apply dataSensor retention policy; automatic PostgreSQL retention stays inactive.");
            }
        }

        // Instance, not static - the GitHub-repository fallback below reads the injected settings.
        private ServerConfig ToDto(ServerConfigRow r) => new()
        {
            IDServerConfig = r.IDServerConfig,
            PortHTTP = r.PortHTTP,
            PortHTTPS = r.PortHTTPS,
            WaterLevelHysteresis = r.WaterLevelHysteresis,
            TemperatureHysteresis = r.TemperatureHysteresis,
            HumidityHysteresis = r.HumidityHysteresis,
            LightHysteresis = r.LightHysteresis,
            BatteryLowThreshold = r.BatteryLowThreshold,
            BatteryLowHysteresis = r.BatteryLowHysteresis,
            TankRefillThreshold = r.TankRefillThreshold,
            TankRefillHysteresis = r.TankRefillHysteresis,
            WaterPumpMaxRunSeconds = r.WaterPumpMaxRunSeconds,
            WaterPumpCooldownSeconds = r.WaterPumpCooldownSeconds,
            EventDedupeMinutes = r.EventDedupeMinutes,
            ActivationResendCooldownMinutes = r.ActivationResendCooldownMinutes,
            MaxRulesPerZone = r.MaxRulesPerZone ?? settings.MaxRulesPerZone,
            AllowSelfServiceTenantCreation = r.AllowSelfServiceTenantCreation,
            TenantManagementEnabled = r.TenantManagementEnabled,
            FirmwareSource = (FirmwareSource)r.FirmwareSource,
            // An older row has NULL here; falling back to the settings seed beats an empty repository nobody can sync from.
            FirmwareGitHubRepository = string.IsNullOrWhiteSpace(r.FirmwareGitHubRepository) ? settings.FirmwareGitHubRepository : r.FirmwareGitHubRepository,
            FirmwareCustomRepositoryUrl = r.FirmwareCustomRepositoryUrl,
            // An older row has NULL here - same appsettings-seed fallback as FirmwareGitHubRepository, rather than showing "disabled" for every existing install.
            FirmwareRefreshIntervalHours = r.FirmwareRefreshIntervalHours ?? settings.FirmwareRefreshIntervalHours,
            FirmwareLastRefreshedAtUtc = r.FirmwareLastRefreshedAtUtc,
            SensorDataRetentionDays = r.SensorDataRetentionDays,
            RecycleBinRetentionDays = r.RecycleBinRetentionDays ?? 30,
            SatelliteRasterRetentionDays = r.SatelliteRasterRetentionDays ?? 30,
            PurgeOrphanedSensorDataScheduleEnabled = r.PurgeOrphanedSensorDataScheduleEnabled,
            WeatherLocationLat = r.WeatherLocationLat,
            WeatherLocationLon = r.WeatherLocationLon,
            // An older row has NULL here - same appsettings-seed fallback as FirmwareGitHubRepository, rather than surfacing an empty interval/threshold.
            WeatherPollIntervalMinutes = r.WeatherPollIntervalMinutes ?? settings.WeatherPollIntervalMinutes,
            WeatherRainSkipThreshold = r.WeatherRainSkipThreshold ?? settings.WeatherRainSkipThreshold,
            // Same appsettings-seed fallback as the WeatherPollIntervalMinutes/WeatherRainSkipThreshold pair above.
            FrostLookaheadHours = r.FrostLookaheadHours ?? settings.FrostLookaheadHours,
            FrostTempThresholdC = r.FrostTempThresholdC ?? settings.FrostTempThresholdC,
            FrostCloudinessMaxPercent = r.FrostCloudinessMaxPercent ?? settings.FrostCloudinessMaxPercent,
            FrostWindMaxMetersPerSecond = r.FrostWindMaxMetersPerSecond ?? settings.FrostWindMaxMetersPerSecond,
            ODataEnabled = r.ODataEnabled,
            ArkodGeoPackageSyncEnabled = r.ArkodGeoPackageSyncEnabled,
            ArkodGeoPackageSyncedAtUtc = r.ArkodGeoPackageSyncedAtUtc,
            GatewayEnabled = r.GatewayEnabled,
            GatewayMode = (GatewayMode)r.GatewayMode,
            // An older row has 0 here, which already equals the sane default (a 10-300 clamp keeps 0 unreachable otherwise) - no settings.* fallback needed.
            GatewayWaitWindowSeconds = r.GatewayWaitWindowSeconds == 0 ? 30 : r.GatewayWaitWindowSeconds,
            ProblemEventAlertsEnabled = r.ProblemEventAlertsEnabled,
            // An older row has 0 here (column default backfills real rows to 24; the generated-default path above already sets it explicitly).
            ProblemEventExpiryHours = r.ProblemEventExpiryHours == 0 ? 24 : r.ProblemEventExpiryHours,
            // An older row has 0 here, which is not a usable minimum length - same 0-means-unset fallback as GatewayWaitWindowSeconds.
            PasswordMinLength = r.PasswordMinLength == 0 ? 8 : r.PasswordMinLength,
            PasswordRequireComplexity = r.PasswordRequireComplexity,
            ConfigHeartbeatHours = r.ConfigHeartbeatHours,
            MqttTransportEnabled = r.MqttTransportEnabled,
            MqttBrokerHost = r.MqttBrokerHost,
            MqttBrokerPort = r.MqttBrokerPort == 0 ? 1883 : r.MqttBrokerPort,
            MqttUsername = r.MqttUsername,
            // Real value, decrypted - MqttCommandPublisher needs it to actually authenticate. The API/edit-form response redacts this at the controller boundary (ServerConfigApiController.Get), not here.
            MqttPassword = secretProtector.Unprotect(r.MqttPassword),
            // Written directly by tools/Agrumy.MqttCredentialSync, never through this Update path - read-only here.
            MqttCredentialsSyncedAtUtc = r.MqttCredentialsSyncedAtUtc,
            EmailEnabled = r.EmailEnabled,
            EmailHost = r.EmailHost,
            // An older row has 0 here, which is not a usable port - same 0-means-unset fallback as GatewayWaitWindowSeconds.
            EmailPort = r.EmailPort == 0 ? 587 : r.EmailPort,
            EmailUseStartTls = r.EmailUseStartTls,
            EmailUsername = r.EmailUsername,
            EmailFromAddress = r.EmailFromAddress,
            EmailFromName = string.IsNullOrWhiteSpace(r.EmailFromName) ? "Agrumy" : r.EmailFromName,
            // Real value, decrypted - same reasoning as MqttPassword above.
            EmailPassword = secretProtector.Unprotect(r.EmailPassword),
            WebhookEnabled = r.WebhookEnabled,
            WebhookUrl = r.WebhookUrl,
            // Real value, decrypted - same reasoning as MqttPassword/EmailPassword above.
            WebhookSecret = secretProtector.Unprotect(r.WebhookSecret),
            // An older row has 0 here, which is not a usable duration - same 0-means-unset fallback as GatewayWaitWindowSeconds.
            DevicePinValidMinutes = r.DevicePinValidMinutes == 0 ? 60 : r.DevicePinValidMinutes,
            ArchiveEnabled = r.ArchiveEnabled,
            ArchiveCutoffMode = (ArchiveCutoffMode)r.ArchiveCutoffMode,
            ArchiveCustomCutoffDate = r.ArchiveCustomCutoffDate,
            ArchiveCustomRollingDays = r.ArchiveCustomRollingDays,
            ArchiveHost = r.ArchiveHost,
            ArchivePort = r.ArchivePort is null or 0 ? 3306 : r.ArchivePort,
            ArchiveDatabaseName = r.ArchiveDatabaseName,
            ArchiveUsername = r.ArchiveUsername,
            // Real value, decrypted - SensorDataArchiveEvaluator needs it to actually connect. The API/edit-form response redacts this at the controller boundary (ServerConfigApiController.Get), same as MqttPassword/EmailPassword above.
            ArchivePassword = secretProtector.Unprotect(r.ArchivePassword),
            ArchiveLastRunAtUtc = r.ArchiveLastRunAtUtc,
        };
    }
}
