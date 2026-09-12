using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class ReorderServerConfigTenantColumns : Migration
    {
        // Postgres has no MODIFY COLUMN ... AFTER (column position is permanent once added) - a purely cosmetic
        // reorder therefore means rename-recreate-copy-drop instead of the single ALTER MySQL gets away with.
        private const string NewOrderColumns =
            "\"IDServerConfig\", \"JWTKey\", \"PortHTTP\", \"PortHTTPS\", \"AllowSelfServiceTenantCreation\", \"TenantManagementEnabled\", " +
            "\"WaterLevelHysteresis\", \"TemperatureHysteresis\", \"HumidityHysteresis\", \"LightHysteresis\", \"BatteryLowThreshold\", " +
            "\"BatteryLowHysteresis\", \"TankRefillThreshold\", \"TankRefillHysteresis\", \"WaterPumpMaxRunSeconds\", \"WaterPumpCooldownSeconds\", " +
            "\"EventDedupeMinutes\", \"ActivationResendCooldownMinutes\", \"MaxRulesPerZone\", \"FirmwareSource\", \"FirmwareGitHubRepository\", " +
            "\"FirmwareCustomRepositoryUrl\", \"FirmwareRefreshIntervalHours\", \"FirmwareLastRefreshedAtUtc\", \"SensorDataRetentionDays\", " +
            "\"RecycleBinRetentionDays\", \"PurgeOrphanedSensorDataScheduleEnabled\", \"WeatherLocationLat\", \"WeatherLocationLon\", " +
            "\"WeatherPollIntervalMinutes\", \"WeatherRainSkipThreshold\", \"GatewayEnabled\", \"GatewayMode\", \"GatewayWaitWindowSeconds\", " +
            "\"ProblemEventAlertsEnabled\", \"ProblemEventExpiryHours\", \"PasswordMinLength\", \"PasswordRequireComplexity\", \"ConfigHeartbeatHours\", " +
            "\"MqttTransportEnabled\", \"MqttBrokerHost\", \"MqttBrokerPort\", \"MqttUsername\", \"MqttPassword\", \"EmailEnabled\", \"EmailHost\", " +
            "\"EmailPort\", \"EmailUseStartTls\", \"EmailUsername\", \"EmailPassword\", \"EmailFromAddress\", \"EmailFromName\", \"DevicePinValidMinutes\", " +
            "\"ArchiveEnabled\", \"ArchiveCutoffMode\", \"ArchiveCustomCutoffDate\", \"ArchiveCustomRollingDays\", \"ArchiveHost\", \"ArchivePort\", " +
            "\"ArchiveDatabaseName\", \"ArchiveUsername\", \"ArchivePassword\", \"ArchiveLastRunAtUtc\", \"WebhookEnabled\", \"WebhookSecret\", " +
            "\"WebhookUrl\", \"MqttCredentialsSyncedAtUtc\", \"FrostCloudinessMaxPercent\", \"FrostLookaheadHours\", \"FrostTempThresholdC\", " +
            "\"FrostWindMaxMetersPerSecond\", \"ODataEnabled\", \"SatelliteRasterRetentionDays\", \"ArkodGeoPackageSyncEnabled\", \"ArkodGeoPackageSyncedAtUtc\"";

        private const string OldOrderColumns =
            "\"IDServerConfig\", \"JWTKey\", \"PortHTTP\", \"PortHTTPS\", " +
            "\"WaterLevelHysteresis\", \"TemperatureHysteresis\", \"HumidityHysteresis\", \"LightHysteresis\", \"BatteryLowThreshold\", " +
            "\"BatteryLowHysteresis\", \"TankRefillThreshold\", \"TankRefillHysteresis\", \"WaterPumpMaxRunSeconds\", \"WaterPumpCooldownSeconds\", " +
            "\"EventDedupeMinutes\", \"ActivationResendCooldownMinutes\", \"MaxRulesPerZone\", \"AllowSelfServiceTenantCreation\", \"TenantManagementEnabled\", " +
            "\"FirmwareSource\", \"FirmwareGitHubRepository\", " +
            "\"FirmwareCustomRepositoryUrl\", \"FirmwareRefreshIntervalHours\", \"FirmwareLastRefreshedAtUtc\", \"SensorDataRetentionDays\", " +
            "\"RecycleBinRetentionDays\", \"PurgeOrphanedSensorDataScheduleEnabled\", \"WeatherLocationLat\", \"WeatherLocationLon\", " +
            "\"WeatherPollIntervalMinutes\", \"WeatherRainSkipThreshold\", \"GatewayEnabled\", \"GatewayMode\", \"GatewayWaitWindowSeconds\", " +
            "\"ProblemEventAlertsEnabled\", \"ProblemEventExpiryHours\", \"PasswordMinLength\", \"PasswordRequireComplexity\", \"ConfigHeartbeatHours\", " +
            "\"MqttTransportEnabled\", \"MqttBrokerHost\", \"MqttBrokerPort\", \"MqttUsername\", \"MqttPassword\", \"EmailEnabled\", \"EmailHost\", " +
            "\"EmailPort\", \"EmailUseStartTls\", \"EmailUsername\", \"EmailPassword\", \"EmailFromAddress\", \"EmailFromName\", \"DevicePinValidMinutes\", " +
            "\"ArchiveEnabled\", \"ArchiveCutoffMode\", \"ArchiveCustomCutoffDate\", \"ArchiveCustomRollingDays\", \"ArchiveHost\", \"ArchivePort\", " +
            "\"ArchiveDatabaseName\", \"ArchiveUsername\", \"ArchivePassword\", \"ArchiveLastRunAtUtc\", \"WebhookEnabled\", \"WebhookSecret\", " +
            "\"WebhookUrl\", \"MqttCredentialsSyncedAtUtc\", \"FrostCloudinessMaxPercent\", \"FrostLookaheadHours\", \"FrostTempThresholdC\", " +
            "\"FrostWindMaxMetersPerSecond\", \"ODataEnabled\", \"SatelliteRasterRetentionDays\", \"ArkodGeoPackageSyncEnabled\", \"ArkodGeoPackageSyncedAtUtc\"";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"serverConfig\" RENAME TO \"serverConfig_old\";");
            migrationBuilder.Sql("""
                CREATE TABLE "serverConfig" (
                    "IDServerConfig" integer NOT NULL,
                    "JWTKey" text,
                    "PortHTTP" integer,
                    "PortHTTPS" integer,
                    "AllowSelfServiceTenantCreation" boolean NOT NULL,
                    "TenantManagementEnabled" boolean NOT NULL,
                    "WaterLevelHysteresis" double precision,
                    "TemperatureHysteresis" double precision,
                    "HumidityHysteresis" double precision,
                    "LightHysteresis" double precision,
                    "BatteryLowThreshold" double precision,
                    "BatteryLowHysteresis" double precision,
                    "TankRefillThreshold" double precision,
                    "TankRefillHysteresis" double precision,
                    "WaterPumpMaxRunSeconds" integer,
                    "WaterPumpCooldownSeconds" integer,
                    "EventDedupeMinutes" integer,
                    "ActivationResendCooldownMinutes" integer,
                    "MaxRulesPerZone" integer,
                    "FirmwareSource" integer NOT NULL,
                    "FirmwareGitHubRepository" character varying(200),
                    "FirmwareCustomRepositoryUrl" character varying(500),
                    "FirmwareRefreshIntervalHours" integer,
                    "FirmwareLastRefreshedAtUtc" timestamp with time zone,
                    "SensorDataRetentionDays" integer,
                    "RecycleBinRetentionDays" integer,
                    "PurgeOrphanedSensorDataScheduleEnabled" boolean NOT NULL,
                    "WeatherLocationLat" double precision,
                    "WeatherLocationLon" double precision,
                    "WeatherPollIntervalMinutes" integer,
                    "WeatherRainSkipThreshold" double precision,
                    "GatewayEnabled" boolean NOT NULL,
                    "GatewayMode" integer NOT NULL,
                    "GatewayWaitWindowSeconds" integer NOT NULL,
                    "ProblemEventAlertsEnabled" boolean NOT NULL,
                    "ProblemEventExpiryHours" integer NOT NULL,
                    "PasswordMinLength" integer NOT NULL,
                    "PasswordRequireComplexity" boolean NOT NULL,
                    "ConfigHeartbeatHours" integer NOT NULL,
                    "MqttTransportEnabled" boolean NOT NULL,
                    "MqttBrokerHost" text,
                    "MqttBrokerPort" integer NOT NULL,
                    "MqttUsername" text,
                    "MqttPassword" character varying(512),
                    "EmailEnabled" boolean NOT NULL,
                    "EmailHost" text,
                    "EmailPort" integer NOT NULL,
                    "EmailUseStartTls" boolean NOT NULL,
                    "EmailUsername" text,
                    "EmailPassword" character varying(512),
                    "EmailFromAddress" text,
                    "EmailFromName" text NOT NULL,
                    "DevicePinValidMinutes" integer NOT NULL,
                    "ArchiveEnabled" boolean NOT NULL,
                    "ArchiveCutoffMode" integer NOT NULL,
                    "ArchiveCustomCutoffDate" date,
                    "ArchiveCustomRollingDays" integer,
                    "ArchiveHost" character varying(255),
                    "ArchivePort" integer,
                    "ArchiveDatabaseName" character varying(64),
                    "ArchiveUsername" character varying(128),
                    "ArchivePassword" character varying(512),
                    "ArchiveLastRunAtUtc" timestamp with time zone,
                    "WebhookEnabled" boolean NOT NULL DEFAULT FALSE,
                    "WebhookSecret" character varying(512),
                    "WebhookUrl" character varying(2048),
                    "MqttCredentialsSyncedAtUtc" timestamp with time zone,
                    "FrostCloudinessMaxPercent" double precision,
                    "FrostLookaheadHours" integer,
                    "FrostTempThresholdC" double precision,
                    "FrostWindMaxMetersPerSecond" double precision,
                    "ODataEnabled" boolean NOT NULL DEFAULT FALSE,
                    "SatelliteRasterRetentionDays" integer,
                    "ArkodGeoPackageSyncEnabled" boolean NOT NULL DEFAULT FALSE,
                    "ArkodGeoPackageSyncedAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_serverConfig" PRIMARY KEY ("IDServerConfig")
                );
                """);
            migrationBuilder.Sql($"INSERT INTO \"serverConfig\" ({NewOrderColumns}) SELECT {NewOrderColumns} FROM \"serverConfig_old\";");
            migrationBuilder.Sql("DROP TABLE \"serverConfig_old\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"serverConfig\" RENAME TO \"serverConfig_old\";");
            migrationBuilder.Sql("""
                CREATE TABLE "serverConfig" (
                    "IDServerConfig" integer NOT NULL,
                    "JWTKey" text,
                    "PortHTTP" integer,
                    "PortHTTPS" integer,
                    "WaterLevelHysteresis" double precision,
                    "TemperatureHysteresis" double precision,
                    "HumidityHysteresis" double precision,
                    "LightHysteresis" double precision,
                    "BatteryLowThreshold" double precision,
                    "BatteryLowHysteresis" double precision,
                    "TankRefillThreshold" double precision,
                    "TankRefillHysteresis" double precision,
                    "WaterPumpMaxRunSeconds" integer,
                    "WaterPumpCooldownSeconds" integer,
                    "EventDedupeMinutes" integer,
                    "ActivationResendCooldownMinutes" integer,
                    "MaxRulesPerZone" integer,
                    "AllowSelfServiceTenantCreation" boolean NOT NULL,
                    "TenantManagementEnabled" boolean NOT NULL,
                    "FirmwareSource" integer NOT NULL,
                    "FirmwareGitHubRepository" character varying(200),
                    "FirmwareCustomRepositoryUrl" character varying(500),
                    "FirmwareRefreshIntervalHours" integer,
                    "FirmwareLastRefreshedAtUtc" timestamp with time zone,
                    "SensorDataRetentionDays" integer,
                    "RecycleBinRetentionDays" integer,
                    "PurgeOrphanedSensorDataScheduleEnabled" boolean NOT NULL,
                    "WeatherLocationLat" double precision,
                    "WeatherLocationLon" double precision,
                    "WeatherPollIntervalMinutes" integer,
                    "WeatherRainSkipThreshold" double precision,
                    "GatewayEnabled" boolean NOT NULL,
                    "GatewayMode" integer NOT NULL,
                    "GatewayWaitWindowSeconds" integer NOT NULL,
                    "ProblemEventAlertsEnabled" boolean NOT NULL,
                    "ProblemEventExpiryHours" integer NOT NULL,
                    "PasswordMinLength" integer NOT NULL,
                    "PasswordRequireComplexity" boolean NOT NULL,
                    "ConfigHeartbeatHours" integer NOT NULL,
                    "MqttTransportEnabled" boolean NOT NULL,
                    "MqttBrokerHost" text,
                    "MqttBrokerPort" integer NOT NULL,
                    "MqttUsername" text,
                    "MqttPassword" character varying(512),
                    "EmailEnabled" boolean NOT NULL,
                    "EmailHost" text,
                    "EmailPort" integer NOT NULL,
                    "EmailUseStartTls" boolean NOT NULL,
                    "EmailUsername" text,
                    "EmailPassword" character varying(512),
                    "EmailFromAddress" text,
                    "EmailFromName" text NOT NULL,
                    "DevicePinValidMinutes" integer NOT NULL,
                    "ArchiveEnabled" boolean NOT NULL,
                    "ArchiveCutoffMode" integer NOT NULL,
                    "ArchiveCustomCutoffDate" date,
                    "ArchiveCustomRollingDays" integer,
                    "ArchiveHost" character varying(255),
                    "ArchivePort" integer,
                    "ArchiveDatabaseName" character varying(64),
                    "ArchiveUsername" character varying(128),
                    "ArchivePassword" character varying(512),
                    "ArchiveLastRunAtUtc" timestamp with time zone,
                    "WebhookEnabled" boolean NOT NULL DEFAULT FALSE,
                    "WebhookSecret" character varying(512),
                    "WebhookUrl" character varying(2048),
                    "MqttCredentialsSyncedAtUtc" timestamp with time zone,
                    "FrostCloudinessMaxPercent" double precision,
                    "FrostLookaheadHours" integer,
                    "FrostTempThresholdC" double precision,
                    "FrostWindMaxMetersPerSecond" double precision,
                    "ODataEnabled" boolean NOT NULL DEFAULT FALSE,
                    "SatelliteRasterRetentionDays" integer,
                    "ArkodGeoPackageSyncEnabled" boolean NOT NULL DEFAULT FALSE,
                    "ArkodGeoPackageSyncedAtUtc" timestamp with time zone,
                    CONSTRAINT "PK_serverConfig" PRIMARY KEY ("IDServerConfig")
                );
                """);
            migrationBuilder.Sql($"INSERT INTO \"serverConfig\" ({OldOrderColumns}) SELECT {OldOrderColumns} FROM \"serverConfig_old\";");
            migrationBuilder.Sql("DROP TABLE \"serverConfig_old\";");
        }
    }
}
