using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Agrumy.Api.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialBeta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auditLog",
                columns: table => new
                {
                    IDAuditLog = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    ActorUserID = table.Column<int>(type: "integer", nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TargetId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auditLog", x => x.IDAuditLog);
                });

            migrationBuilder.CreateTable(
                name: "deviceConfigController",
                columns: table => new
                {
                    IDDeviceConfigController = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TempLow = table.Column<double>(type: "double precision", nullable: true),
                    TempHigh = table.Column<double>(type: "double precision", nullable: true),
                    HumidLow = table.Column<double>(type: "double precision", nullable: true),
                    HumidHigh = table.Column<double>(type: "double precision", nullable: true),
                    MoistLow = table.Column<double>(type: "double precision", nullable: true),
                    MoistHigh = table.Column<double>(type: "double precision", nullable: true),
                    LightLow = table.Column<double>(type: "double precision", nullable: true),
                    LightHigh = table.Column<double>(type: "double precision", nullable: true),
                    WaterLow = table.Column<double>(type: "double precision", nullable: true),
                    WaterHigh = table.Column<double>(type: "double precision", nullable: true),
                    WaterLevelHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    TemperatureHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    HumidityHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    LightHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    VentilationIntervalEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    VentilationInterval = table.Column<int>(type: "integer", nullable: true),
                    VentilationIntervalLength = table.Column<int>(type: "integer", nullable: true),
                    LightIntervalEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    LightInterval = table.Column<int>(type: "integer", nullable: true),
                    LightIntervalLength = table.Column<int>(type: "integer", nullable: true),
                    HeatingIntervalEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    HeatingInterval = table.Column<int>(type: "integer", nullable: true),
                    HeatingIntervalLength = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpIntervalEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    WaterPumpInterval = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpIntervalLength = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpCooldownSeconds = table.Column<int>(type: "integer", nullable: true),
                    RelayEnabled = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceConfigController", x => x.IDDeviceConfigController);
                });

            migrationBuilder.CreateTable(
                name: "deviceFarm",
                columns: table => new
                {
                    IDDeviceFarm = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceFarm", x => x.IDDeviceFarm);
                });

            migrationBuilder.CreateTable(
                name: "deviceFirmware",
                columns: table => new
                {
                    IDDeviceFirmware = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceTypeID = table.Column<int>(type: "integer", nullable: true),
                    Board = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DateAdded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    FullImageFileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FullImageUrl = table.Column<string>(type: "text", nullable: true),
                    FullImageSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FullImageSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceFirmware", x => x.IDDeviceFirmware);
                });

            migrationBuilder.CreateTable(
                name: "deviceRole",
                columns: table => new
                {
                    IDDeviceRole = table.Column<int>(type: "integer", nullable: false),
                    DeviceRoleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SensorEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    ControllerEnabled = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceRole", x => x.IDDeviceRole);
                });

            migrationBuilder.CreateTable(
                name: "deviceType",
                columns: table => new
                {
                    IDDeviceType = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ControllerCapable = table.Column<bool>(type: "boolean", nullable: false),
                    PinoutJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceType", x => x.IDDeviceType);
                });

            migrationBuilder.CreateTable(
                name: "deviceTypeRelay",
                columns: table => new
                {
                    IDDeviceTypeRelay = table.Column<int>(type: "integer", nullable: false),
                    RelayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceTypeRelay", x => x.IDDeviceTypeRelay);
                });

            migrationBuilder.CreateTable(
                name: "deviceTypeSensor",
                columns: table => new
                {
                    IDDeviceTypeSensor = table.Column<int>(type: "integer", nullable: false),
                    SensorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SensorDescription = table.Column<string>(type: "text", nullable: true),
                    Battery = table.Column<int>(type: "integer", nullable: true),
                    Temperature = table.Column<int>(type: "integer", nullable: true),
                    TemperatureSoil = table.Column<int>(type: "integer", nullable: true),
                    Humidity = table.Column<int>(type: "integer", nullable: true),
                    Moisture = table.Column<int>(type: "integer", nullable: true),
                    Light = table.Column<int>(type: "integer", nullable: true),
                    Co2 = table.Column<int>(type: "integer", nullable: true),
                    Tvoc = table.Column<int>(type: "integer", nullable: true),
                    Barometer = table.Column<int>(type: "integer", nullable: true),
                    WaterPH = table.Column<int>(type: "integer", nullable: true),
                    WaterTankLevel = table.Column<int>(type: "integer", nullable: true),
                    RainLevel = table.Column<int>(type: "integer", nullable: true),
                    Wind = table.Column<int>(type: "integer", nullable: true),
                    Ec = table.Column<int>(type: "integer", nullable: true),
                    Weight = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceTypeSensor", x => x.IDDeviceTypeSensor);
                });

            migrationBuilder.CreateTable(
                name: "deviceTypeService",
                columns: table => new
                {
                    IDDeviceTypeService = table.Column<int>(type: "integer", nullable: false),
                    ServiceType = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceTypeService", x => x.IDDeviceTypeService);
                });

            migrationBuilder.CreateTable(
                name: "eventType",
                columns: table => new
                {
                    IDEventType = table.Column<int>(type: "integer", nullable: false),
                    EventTypeName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventType", x => x.IDEventType);
                });

            migrationBuilder.CreateTable(
                name: "serverConfig",
                columns: table => new
                {
                    IDServerConfig = table.Column<int>(type: "integer", nullable: false),
                    JWTKey = table.Column<string>(type: "text", nullable: true),
                    PortHTTP = table.Column<int>(type: "integer", nullable: true),
                    PortHTTPS = table.Column<int>(type: "integer", nullable: true),
                    WaterLevelHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    TemperatureHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    HumidityHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    LightHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    BatteryLowThreshold = table.Column<double>(type: "double precision", nullable: true),
                    BatteryLowHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    TankRefillThreshold = table.Column<double>(type: "double precision", nullable: true),
                    TankRefillHysteresis = table.Column<double>(type: "double precision", nullable: true),
                    WaterPumpMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpCooldownSeconds = table.Column<int>(type: "integer", nullable: true),
                    EventDedupeMinutes = table.Column<int>(type: "integer", nullable: true),
                    ActivationResendCooldownMinutes = table.Column<int>(type: "integer", nullable: true),
                    MaxRulesPerZone = table.Column<int>(type: "integer", nullable: true),
                    AllowSelfServiceTenantCreation = table.Column<bool>(type: "boolean", nullable: false),
                    TenantManagementEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FirmwareSource = table.Column<int>(type: "integer", nullable: false),
                    FirmwareGitHubRepository = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FirmwareCustomRepositoryUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirmwareRefreshIntervalHours = table.Column<int>(type: "integer", nullable: true),
                    FirmwareLastRefreshedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SensorDataRetentionDays = table.Column<int>(type: "integer", nullable: true),
                    RecycleBinRetentionDays = table.Column<int>(type: "integer", nullable: true),
                    PurgeOrphanedSensorDataScheduleEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WeatherLocationLat = table.Column<double>(type: "double precision", nullable: true),
                    WeatherLocationLon = table.Column<double>(type: "double precision", nullable: true),
                    WeatherPollIntervalMinutes = table.Column<int>(type: "integer", nullable: true),
                    WeatherRainSkipThreshold = table.Column<double>(type: "double precision", nullable: true),
                    WeatherRainPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    WeatherCheckedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GatewayEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GatewayMode = table.Column<int>(type: "integer", nullable: false),
                    GatewayWaitWindowSeconds = table.Column<int>(type: "integer", nullable: false),
                    ProblemEventAlertsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ProblemEventExpiryHours = table.Column<int>(type: "integer", nullable: false),
                    PasswordMinLength = table.Column<int>(type: "integer", nullable: false),
                    PasswordRequireComplexity = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigHeartbeatHours = table.Column<int>(type: "integer", nullable: false),
                    MqttTransportEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MqttBrokerHost = table.Column<string>(type: "text", nullable: true),
                    MqttBrokerPort = table.Column<int>(type: "integer", nullable: false),
                    MqttUsername = table.Column<string>(type: "text", nullable: true),
                    MqttPassword = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailHost = table.Column<string>(type: "text", nullable: true),
                    EmailPort = table.Column<int>(type: "integer", nullable: false),
                    EmailUseStartTls = table.Column<bool>(type: "boolean", nullable: false),
                    EmailUsername = table.Column<string>(type: "text", nullable: true),
                    EmailPassword = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EmailFromAddress = table.Column<string>(type: "text", nullable: true),
                    EmailFromName = table.Column<string>(type: "text", nullable: false),
                    DevicePinValidMinutes = table.Column<int>(type: "integer", nullable: false),
                    ArchiveEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ArchiveCutoffMode = table.Column<int>(type: "integer", nullable: false),
                    ArchiveCustomCutoffDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ArchiveCustomRollingDays = table.Column<int>(type: "integer", nullable: true),
                    ArchiveHost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ArchivePort = table.Column<int>(type: "integer", nullable: true),
                    ArchiveDatabaseName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ArchiveUsername = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ArchivePassword = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ArchiveLastRunAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_serverConfig", x => x.IDServerConfig);
                });

            migrationBuilder.CreateTable(
                name: "simulationSession",
                columns: table => new
                {
                    IDSimulationSession = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StoppedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulationSession", x => x.IDSimulationSession);
                });

            migrationBuilder.CreateTable(
                name: "tenant",
                columns: table => new
                {
                    IDTenant = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScheduleTimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    EmergencyStopActive = table.Column<bool>(type: "boolean", nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant", x => x.IDTenant);
                });

            migrationBuilder.CreateTable(
                name: "tenantWifiConfig",
                columns: table => new
                {
                    IDTenantWifiConfig = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    Ssid = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Password = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenantWifiConfig", x => x.IDTenantWifiConfig);
                });

            migrationBuilder.CreateTable(
                name: "user",
                columns: table => new
                {
                    IDUser = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PwdHash = table.Column<string>(type: "text", nullable: true),
                    PwdSalt = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BootstrapSecretHash = table.Column<string>(type: "text", nullable: true),
                    BootstrapSecretSalt = table.Column<string>(type: "text", nullable: true),
                    DevicePin = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    DevicePinExpires = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DateModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ActivationTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActivationTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivationLastSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    TokensValidAfterUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user", x => x.IDUser);
                });

            migrationBuilder.CreateTable(
                name: "userRoleScope",
                columns: table => new
                {
                    IDRoleScope = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleScopeName = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userRoleScope", x => x.IDRoleScope);
                });

            migrationBuilder.CreateTable(
                name: "deviceScheduleSlot",
                columns: table => new
                {
                    IDDeviceScheduleSlot = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceConfigControllerID = table.Column<int>(type: "integer", nullable: false),
                    RelayFunction = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeek = table.Column<int>(type: "integer", nullable: false),
                    Start = table.Column<int>(type: "integer", nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceScheduleSlot", x => x.IDDeviceScheduleSlot);
                    table.ForeignKey(
                        name: "FK_deviceScheduleSlot_deviceConfigController_DeviceConfigContr~",
                        column: x => x.DeviceConfigControllerID,
                        principalTable: "deviceConfigController",
                        principalColumn: "IDDeviceConfigController");
                });

            migrationBuilder.CreateTable(
                name: "deviceFarmUnit",
                columns: table => new
                {
                    IDDeviceFarmUnit = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ZoneEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    DeviceFarmID = table.Column<int>(type: "integer", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceFarmUnit", x => x.IDDeviceFarmUnit);
                    table.ForeignKey(
                        name: "FK_deviceFarmUnit_deviceFarm_DeviceFarmID",
                        column: x => x.DeviceFarmID,
                        principalTable: "deviceFarm",
                        principalColumn: "IDDeviceFarm");
                });

            migrationBuilder.CreateTable(
                name: "deviceConfigControllerRelay",
                columns: table => new
                {
                    IDDeviceConfigController = table.Column<int>(type: "integer", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    RelayFunction = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceConfigControllerRelay", x => new { x.IDDeviceConfigController, x.Slot });
                    table.ForeignKey(
                        name: "FK_deviceConfigControllerRelay_deviceConfigController_IDDevice~",
                        column: x => x.IDDeviceConfigController,
                        principalTable: "deviceConfigController",
                        principalColumn: "IDDeviceConfigController",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_deviceConfigControllerRelay_deviceTypeRelay_RelayFunction",
                        column: x => x.RelayFunction,
                        principalTable: "deviceTypeRelay",
                        principalColumn: "IDDeviceTypeRelay");
                });

            migrationBuilder.CreateTable(
                name: "deviceConfigSensor",
                columns: table => new
                {
                    IDDeviceConfigSensor = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SensorBattery = table.Column<int>(type: "integer", nullable: true),
                    BatteryDividerR1 = table.Column<double>(type: "double precision", nullable: true),
                    BatteryDividerR2 = table.Column<double>(type: "double precision", nullable: true),
                    SensorTemp = table.Column<int>(type: "integer", nullable: true),
                    SensorTempSoil = table.Column<int>(type: "integer", nullable: true),
                    SensorHumid = table.Column<int>(type: "integer", nullable: true),
                    SensorMoist = table.Column<int>(type: "integer", nullable: true),
                    SensorLight = table.Column<int>(type: "integer", nullable: true),
                    SensorCo2 = table.Column<int>(type: "integer", nullable: true),
                    SensorTvoc = table.Column<int>(type: "integer", nullable: true),
                    SensorBarometer = table.Column<int>(type: "integer", nullable: true),
                    SensorPH = table.Column<int>(type: "integer", nullable: true),
                    SensorRainLevel = table.Column<int>(type: "integer", nullable: true),
                    SensorWaterLevel = table.Column<int>(type: "integer", nullable: true),
                    SensorWind = table.Column<int>(type: "integer", nullable: true),
                    SensorEc = table.Column<int>(type: "integer", nullable: true),
                    SensorWeight = table.Column<int>(type: "integer", nullable: true),
                    WeightCalibrationFactor = table.Column<double>(type: "double precision", nullable: true),
                    EcCalibrationSlope = table.Column<double>(type: "double precision", nullable: true),
                    EcCalibrationOffset = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceConfigSensor", x => x.IDDeviceConfigSensor);
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorBarometer",
                        column: x => x.SensorBarometer,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorBattery",
                        column: x => x.SensorBattery,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorCo2",
                        column: x => x.SensorCo2,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorEc",
                        column: x => x.SensorEc,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorHumid",
                        column: x => x.SensorHumid,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorLight",
                        column: x => x.SensorLight,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorMoist",
                        column: x => x.SensorMoist,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorPH",
                        column: x => x.SensorPH,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorRainLevel",
                        column: x => x.SensorRainLevel,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorTemp",
                        column: x => x.SensorTemp,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorTempSoil",
                        column: x => x.SensorTempSoil,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorTvoc",
                        column: x => x.SensorTvoc,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorWaterLevel",
                        column: x => x.SensorWaterLevel,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorWeight",
                        column: x => x.SensorWeight,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                    table.ForeignKey(
                        name: "FK_deviceConfigSensor_deviceTypeSensor_SensorWind",
                        column: x => x.SensorWind,
                        principalTable: "deviceTypeSensor",
                        principalColumn: "IDDeviceTypeSensor");
                });

            migrationBuilder.CreateTable(
                name: "eventDevice",
                columns: table => new
                {
                    IDEventDevice = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    EventID = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventDevice", x => x.IDEventDevice);
                    table.ForeignKey(
                        name: "FK_eventDevice_eventType_EventID",
                        column: x => x.EventID,
                        principalTable: "eventType",
                        principalColumn: "IDEventType");
                });

            migrationBuilder.CreateTable(
                name: "eventService",
                columns: table => new
                {
                    IDEventService = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ServiceID = table.Column<int>(type: "integer", nullable: false),
                    EventID = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eventService", x => x.IDEventService);
                    table.ForeignKey(
                        name: "FK_eventService_deviceTypeService_ServiceID",
                        column: x => x.ServiceID,
                        principalTable: "deviceTypeService",
                        principalColumn: "IDDeviceTypeService");
                    table.ForeignKey(
                        name: "FK_eventService_eventType_EventID",
                        column: x => x.EventID,
                        principalTable: "eventType",
                        principalColumn: "IDEventType");
                });

            migrationBuilder.CreateTable(
                name: "userRefreshToken",
                columns: table => new
                {
                    IDRefreshToken = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userRefreshToken", x => x.IDRefreshToken);
                    table.ForeignKey(
                        name: "FK_userRefreshToken_user_UserID",
                        column: x => x.UserID,
                        principalTable: "user",
                        principalColumn: "IDUser");
                });

            migrationBuilder.CreateTable(
                name: "userRole",
                columns: table => new
                {
                    IDUserRole = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleName = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    RoleScopeID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userRole", x => x.IDUserRole);
                    table.ForeignKey(
                        name: "FK_userRole_userRoleScope_RoleScopeID",
                        column: x => x.RoleScopeID,
                        principalTable: "userRoleScope",
                        principalColumn: "IDRoleScope");
                });

            migrationBuilder.CreateTable(
                name: "deviceFarmUnitZone",
                columns: table => new
                {
                    IDDeviceFarmUnitZone = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitID = table.Column<int>(type: "integer", nullable: false),
                    DeviceFarmUnitZoneName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WaterPumpMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    WaterPumpCooldownSeconds = table.Column<int>(type: "integer", nullable: true),
                    SkipWaterPumpWhenRainPredicted = table.Column<bool>(type: "boolean", nullable: false),
                    TankCapacityLiters = table.Column<double>(type: "double precision", nullable: true),
                    WaterLevelRawEmpty = table.Column<int>(type: "integer", nullable: true),
                    WaterLevelRawFull = table.Column<int>(type: "integer", nullable: true),
                    TankRefillNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WaterPumpMinLevel = table.Column<double>(type: "double precision", nullable: true),
                    HeatingMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    VentilationMaxRunSeconds = table.Column<int>(type: "integer", nullable: true),
                    DashboardWidgetsJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceFarmUnitZone", x => x.IDDeviceFarmUnitZone);
                    table.ForeignKey(
                        name: "FK_deviceFarmUnitZone_deviceFarmUnit_DeviceFarmUnitID",
                        column: x => x.DeviceFarmUnitID,
                        principalTable: "deviceFarmUnit",
                        principalColumn: "IDDeviceFarmUnit");
                });

            migrationBuilder.CreateTable(
                name: "device",
                columns: table => new
                {
                    IDDevice = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    DeviceRoleID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitZoneID = table.Column<int>(type: "integer", nullable: true),
                    DeviceConfigSensorID = table.Column<int>(type: "integer", nullable: true),
                    DeviceConfigControllerID = table.Column<int>(type: "integer", nullable: true),
                    DeviceTypeServiceID = table.Column<int>(type: "integer", nullable: true),
                    DeviceName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MacAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ManualDeviceTypeID = table.Column<int>(type: "integer", nullable: true),
                    ApiId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ServicePoint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ServicePublicKey = table.Column<string>(type: "text", nullable: true),
                    SleepSeconds = table.Column<int>(type: "integer", nullable: true),
                    SleepDeepEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    LoRaGatewayEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    DeviceSensorEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    DeviceControllerEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    BatteryEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: true),
                    Debug = table.Column<bool>(type: "boolean", nullable: true),
                    Reboot = table.Column<bool>(type: "boolean", nullable: true),
                    Reset = table.Column<bool>(type: "boolean", nullable: true),
                    FirmwareUpdate = table.Column<bool>(type: "boolean", nullable: true),
                    FirmwareTargetVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ConfigVersion = table.Column<int>(type: "integer", nullable: true),
                    CommandVersion = table.Column<int>(type: "integer", nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DateModified = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsGateway = table.Column<bool>(type: "boolean", nullable: false),
                    GatewayProfile = table.Column<int>(type: "integer", nullable: true),
                    LastFullConfigSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LoRaPrivateKeyHex = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LoRaLastUplinkCounter = table.Column<long>(type: "bigint", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device", x => x.IDDevice);
                    table.ForeignKey(
                        name: "FK_device_deviceConfigController_DeviceConfigControllerID",
                        column: x => x.DeviceConfigControllerID,
                        principalTable: "deviceConfigController",
                        principalColumn: "IDDeviceConfigController");
                    table.ForeignKey(
                        name: "FK_device_deviceConfigSensor_DeviceConfigSensorID",
                        column: x => x.DeviceConfigSensorID,
                        principalTable: "deviceConfigSensor",
                        principalColumn: "IDDeviceConfigSensor");
                    table.ForeignKey(
                        name: "FK_device_deviceFarmUnit_DeviceFarmUnitID",
                        column: x => x.DeviceFarmUnitID,
                        principalTable: "deviceFarmUnit",
                        principalColumn: "IDDeviceFarmUnit");
                    table.ForeignKey(
                        name: "FK_device_deviceRole_DeviceRoleID",
                        column: x => x.DeviceRoleID,
                        principalTable: "deviceRole",
                        principalColumn: "IDDeviceRole");
                    table.ForeignKey(
                        name: "FK_device_deviceTypeService_DeviceTypeServiceID",
                        column: x => x.DeviceTypeServiceID,
                        principalTable: "deviceTypeService",
                        principalColumn: "IDDeviceTypeService");
                    table.ForeignKey(
                        name: "FK_device_deviceType_ManualDeviceTypeID",
                        column: x => x.ManualDeviceTypeID,
                        principalTable: "deviceType",
                        principalColumn: "IDDeviceType");
                    table.ForeignKey(
                        name: "FK_device_tenant_TenantID",
                        column: x => x.TenantID,
                        principalTable: "tenant",
                        principalColumn: "IDTenant");
                });

            migrationBuilder.CreateTable(
                name: "userUserRole",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "integer", nullable: false),
                    UserRoleID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_userUserRole", x => new { x.UserID, x.UserRoleID });
                    table.ForeignKey(
                        name: "FK_userUserRole_userRole_UserRoleID",
                        column: x => x.UserRoleID,
                        principalTable: "userRole",
                        principalColumn: "IDUserRole");
                    table.ForeignKey(
                        name: "FK_userUserRole_user_UserID",
                        column: x => x.UserID,
                        principalTable: "user",
                        principalColumn: "IDUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deviceFarmUnitZoneRule",
                columns: table => new
                {
                    IDDeviceFarmUnitZoneRule = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    DeviceFarmID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitZoneID = table.Column<int>(type: "integer", nullable: true),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    RelayFunction = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RootConditionJson = table.Column<string>(type: "text", nullable: false),
                    IsSafetyRule = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationSubject = table.Column<string>(type: "text", nullable: true),
                    NotificationBody = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceFarmUnitZoneRule", x => x.IDDeviceFarmUnitZoneRule);
                    table.ForeignKey(
                        name: "FK_deviceFarmUnitZoneRule_deviceFarmUnitZone_DeviceFarmUnitZon~",
                        column: x => x.DeviceFarmUnitZoneID,
                        principalTable: "deviceFarmUnitZone",
                        principalColumn: "IDDeviceFarmUnitZone");
                    table.ForeignKey(
                        name: "FK_deviceFarmUnitZoneRule_deviceFarmUnit_DeviceFarmUnitID",
                        column: x => x.DeviceFarmUnitID,
                        principalTable: "deviceFarmUnit",
                        principalColumn: "IDDeviceFarmUnit");
                    table.ForeignKey(
                        name: "FK_deviceFarmUnitZoneRule_deviceFarm_DeviceFarmID",
                        column: x => x.DeviceFarmID,
                        principalTable: "deviceFarm",
                        principalColumn: "IDDeviceFarm");
                });

            migrationBuilder.CreateTable(
                name: "controllerData",
                columns: table => new
                {
                    IDControllerData = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    RelayFunction = table.Column<int>(type: "integer", nullable: false),
                    IsOn = table.Column<bool>(type: "boolean", nullable: false),
                    DateChanged = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_controllerData", x => x.IDControllerData);
                    table.ForeignKey(
                        name: "FK_controllerData_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "deviceCommand",
                columns: table => new
                {
                    IDDeviceCommand = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExecutedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActiveKey = table.Column<int>(type: "integer", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceCommand", x => x.IDDeviceCommand);
                    table.ForeignKey(
                        name: "FK_deviceCommand_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "deviceDiagnostic",
                columns: table => new
                {
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UptimeSeconds = table.Column<long>(type: "bigint", nullable: true),
                    RssiDbm = table.Column<int>(type: "integer", nullable: true),
                    FreeHeapBytes = table.Column<long>(type: "bigint", nullable: true),
                    OfflineNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LowBatteryNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirmwareVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Board = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DeviceTypeID = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceDiagnostic", x => x.DeviceID);
                    table.ForeignKey(
                        name: "FK_deviceDiagnostic_deviceType_DeviceTypeID",
                        column: x => x.DeviceTypeID,
                        principalTable: "deviceType",
                        principalColumn: "IDDeviceType");
                    table.ForeignKey(
                        name: "FK_deviceDiagnostic_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "deviceDiscoveryReport",
                columns: table => new
                {
                    IDReport = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScanningDeviceID = table.Column<int>(type: "integer", nullable: false),
                    DiscoveredApMac = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Rssi = table.Column<int>(type: "integer", nullable: true),
                    DateReported = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceDiscoveryReport", x => x.IDReport);
                    table.ForeignKey(
                        name: "FK_deviceDiscoveryReport_device_ScanningDeviceID",
                        column: x => x.ScanningDeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "deviceManualOverride",
                columns: table => new
                {
                    IDDeviceManualOverride = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    RelayFunction = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TargetMetric = table.Column<int>(type: "integer", nullable: true),
                    TargetThreshold = table.Column<double>(type: "double precision", nullable: true),
                    TargetHysteresis = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceManualOverride", x => x.IDDeviceManualOverride);
                    table.ForeignKey(
                        name: "FK_deviceManualOverride_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "deviceSimulation",
                columns: table => new
                {
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    SoilTemperature = table.Column<double>(type: "double precision", nullable: true),
                    Humidity = table.Column<double>(type: "double precision", nullable: true),
                    Battery = table.Column<int>(type: "integer", nullable: true),
                    Moisture = table.Column<int>(type: "integer", nullable: true),
                    Light = table.Column<int>(type: "integer", nullable: true),
                    Co2 = table.Column<int>(type: "integer", nullable: true),
                    Tvoc = table.Column<int>(type: "integer", nullable: true),
                    Barometer = table.Column<double>(type: "double precision", nullable: true),
                    LiquidPH = table.Column<double>(type: "double precision", nullable: true),
                    RainLevel = table.Column<int>(type: "integer", nullable: true),
                    WaterLevel = table.Column<int>(type: "integer", nullable: true),
                    Wind = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceSimulation", x => x.DeviceID);
                    table.ForeignKey(
                        name: "FK_deviceSimulation_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "deviceVirtual",
                columns: table => new
                {
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_deviceVirtual", x => x.DeviceID);
                    table.ForeignKey(
                        name: "FK_deviceVirtual_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "gatewayDeviceMapping",
                columns: table => new
                {
                    IDGatewayDeviceMapping = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IDGatewayDevice = table.Column<int>(type: "integer", nullable: false),
                    DevEUI = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IDDevice = table.Column<int>(type: "integer", nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gatewayDeviceMapping", x => x.IDGatewayDeviceMapping);
                    table.ForeignKey(
                        name: "FK_gatewayDeviceMapping_device_IDDevice",
                        column: x => x.IDDevice,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                    table.ForeignKey(
                        name: "FK_gatewayDeviceMapping_device_IDGatewayDevice",
                        column: x => x.IDGatewayDevice,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "sensorData",
                columns: table => new
                {
                    IDSensorData = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantID = table.Column<int>(type: "integer", nullable: false),
                    DeviceID = table.Column<int>(type: "integer", nullable: false),
                    DeviceFarmUnitID = table.Column<int>(type: "integer", nullable: true),
                    DeviceFarmUnitZoneID = table.Column<int>(type: "integer", nullable: true),
                    Battery = table.Column<int>(type: "integer", nullable: true),
                    Temperature = table.Column<double>(type: "double precision", nullable: true),
                    SoilTemperature = table.Column<double>(type: "double precision", nullable: true),
                    Humidity = table.Column<double>(type: "double precision", nullable: true),
                    Moisture = table.Column<int>(type: "integer", nullable: true),
                    Light = table.Column<int>(type: "integer", nullable: true),
                    Co2 = table.Column<int>(type: "integer", nullable: true),
                    Tvoc = table.Column<int>(type: "integer", nullable: true),
                    Barometer = table.Column<double>(type: "double precision", nullable: true),
                    LiquidPH = table.Column<double>(type: "double precision", nullable: true),
                    RainLevel = table.Column<int>(type: "integer", nullable: true),
                    WaterLevel = table.Column<int>(type: "integer", nullable: true),
                    Wind = table.Column<int>(type: "integer", nullable: true),
                    Ec = table.Column<double>(type: "double precision", nullable: true),
                    Weight = table.Column<double>(type: "double precision", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensorData", x => x.IDSensorData);
                    table.ForeignKey(
                        name: "FK_sensorData_deviceFarmUnitZone_DeviceFarmUnitZoneID",
                        column: x => x.DeviceFarmUnitZoneID,
                        principalTable: "deviceFarmUnitZone",
                        principalColumn: "IDDeviceFarmUnitZone");
                    table.ForeignKey(
                        name: "FK_sensorData_deviceFarmUnit_DeviceFarmUnitID",
                        column: x => x.DeviceFarmUnitID,
                        principalTable: "deviceFarmUnit",
                        principalColumn: "IDDeviceFarmUnit");
                    table.ForeignKey(
                        name: "FK_sensorData_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "sensorDataReport",
                columns: table => new
                {
                    IDSensorDataReport = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    deviceID = table.Column<int>(type: "integer", nullable: true),
                    ReportName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DateGenerated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, defaultValueSql: "CURRENT_TIMESTAMP"),
                    sensorData = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensorDataReport", x => x.IDSensorDataReport);
                    table.ForeignKey(
                        name: "FK_sensorDataReport_device_deviceID",
                        column: x => x.deviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                });

            migrationBuilder.CreateTable(
                name: "simulationSessionDevice",
                columns: table => new
                {
                    IDSimulationSession = table.Column<int>(type: "integer", nullable: false),
                    DeviceID = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulationSessionDevice", x => new { x.IDSimulationSession, x.DeviceID });
                    table.ForeignKey(
                        name: "FK_simulationSessionDevice_device_DeviceID",
                        column: x => x.DeviceID,
                        principalTable: "device",
                        principalColumn: "IDDevice");
                    table.ForeignKey(
                        name: "FK_simulationSessionDevice_simulationSession_IDSimulationSessi~",
                        column: x => x.IDSimulationSession,
                        principalTable: "simulationSession",
                        principalColumn: "IDSimulationSession");
                });

            migrationBuilder.CreateTable(
                name: "ruleNotificationState",
                columns: table => new
                {
                    IDRuleNotificationState = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleID = table.Column<int>(type: "integer", nullable: false),
                    DeviceFarmUnitZoneID = table.Column<int>(type: "integer", nullable: false),
                    WasTrue = table.Column<bool>(type: "boolean", nullable: false),
                    LastFiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ruleNotificationState", x => x.IDRuleNotificationState);
                    table.ForeignKey(
                        name: "FK_ruleNotificationState_deviceFarmUnitZoneRule_RuleID",
                        column: x => x.RuleID,
                        principalTable: "deviceFarmUnitZoneRule",
                        principalColumn: "IDDeviceFarmUnitZoneRule");
                    table.ForeignKey(
                        name: "FK_ruleNotificationState_deviceFarmUnitZone_DeviceFarmUnitZone~",
                        column: x => x.DeviceFarmUnitZoneID,
                        principalTable: "deviceFarmUnitZone",
                        principalColumn: "IDDeviceFarmUnitZone");
                });

            migrationBuilder.CreateIndex(
                name: "ix_auditLog_tenant_timestamp",
                table: "auditLog",
                columns: new[] { "TenantID", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "ux_controllerData_device_relayFunction",
                table: "controllerData",
                columns: new[] { "DeviceID", "RelayFunction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ApiID_UNIQUE",
                table: "device",
                column: "ApiId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_device_DeviceConfigControllerID",
                table: "device",
                column: "DeviceConfigControllerID");

            migrationBuilder.CreateIndex(
                name: "IX_device_DeviceConfigSensorID",
                table: "device",
                column: "DeviceConfigSensorID");

            migrationBuilder.CreateIndex(
                name: "IX_device_DeviceFarmUnitID",
                table: "device",
                column: "DeviceFarmUnitID");

            migrationBuilder.CreateIndex(
                name: "IX_device_DeviceRoleID",
                table: "device",
                column: "DeviceRoleID");

            migrationBuilder.CreateIndex(
                name: "IX_device_DeviceTypeServiceID",
                table: "device",
                column: "DeviceTypeServiceID");

            migrationBuilder.CreateIndex(
                name: "IX_device_ManualDeviceTypeID",
                table: "device",
                column: "ManualDeviceTypeID");

            migrationBuilder.CreateIndex(
                name: "ix_device_tenant",
                table: "device",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "MacAddress_TenantID_UNIQUE",
                table: "device",
                columns: new[] { "MacAddress", "TenantID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deviceCommand_device_status",
                table: "deviceCommand",
                columns: new[] { "DeviceID", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_deviceCommand_device_activekey",
                table: "deviceCommand",
                columns: new[] { "DeviceID", "ActiveKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigControllerRelay_RelayFunction",
                table: "deviceConfigControllerRelay",
                column: "RelayFunction");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorBarometer",
                table: "deviceConfigSensor",
                column: "SensorBarometer");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorBattery",
                table: "deviceConfigSensor",
                column: "SensorBattery");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorCo2",
                table: "deviceConfigSensor",
                column: "SensorCo2");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorEc",
                table: "deviceConfigSensor",
                column: "SensorEc");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorHumid",
                table: "deviceConfigSensor",
                column: "SensorHumid");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorLight",
                table: "deviceConfigSensor",
                column: "SensorLight");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorMoist",
                table: "deviceConfigSensor",
                column: "SensorMoist");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorPH",
                table: "deviceConfigSensor",
                column: "SensorPH");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorRainLevel",
                table: "deviceConfigSensor",
                column: "SensorRainLevel");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorTemp",
                table: "deviceConfigSensor",
                column: "SensorTemp");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorTempSoil",
                table: "deviceConfigSensor",
                column: "SensorTempSoil");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorTvoc",
                table: "deviceConfigSensor",
                column: "SensorTvoc");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorWaterLevel",
                table: "deviceConfigSensor",
                column: "SensorWaterLevel");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorWeight",
                table: "deviceConfigSensor",
                column: "SensorWeight");

            migrationBuilder.CreateIndex(
                name: "IX_deviceConfigSensor_SensorWind",
                table: "deviceConfigSensor",
                column: "SensorWind");

            migrationBuilder.CreateIndex(
                name: "IX_deviceDiagnostic_DeviceTypeID",
                table: "deviceDiagnostic",
                column: "DeviceTypeID");

            migrationBuilder.CreateIndex(
                name: "ix_deviceDiscoveryReport_apMac",
                table: "deviceDiscoveryReport",
                column: "DiscoveredApMac");

            migrationBuilder.CreateIndex(
                name: "IX_deviceDiscoveryReport_ScanningDeviceID",
                table: "deviceDiscoveryReport",
                column: "ScanningDeviceID");

            migrationBuilder.CreateIndex(
                name: "IX_deviceFarmUnit_DeviceFarmID",
                table: "deviceFarmUnit",
                column: "DeviceFarmID");

            migrationBuilder.CreateIndex(
                name: "IX_deviceFarmUnitZone_DeviceFarmUnitID",
                table: "deviceFarmUnitZone",
                column: "DeviceFarmUnitID");

            migrationBuilder.CreateIndex(
                name: "ix_deviceFarmUnitZoneRule_farm",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmID");

            migrationBuilder.CreateIndex(
                name: "ix_deviceFarmUnitZoneRule_tenant",
                table: "deviceFarmUnitZoneRule",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "ix_deviceFarmUnitZoneRule_unit",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmUnitID");

            migrationBuilder.CreateIndex(
                name: "ix_deviceFarmUnitZoneRule_zone",
                table: "deviceFarmUnitZoneRule",
                column: "DeviceFarmUnitZoneID");

            migrationBuilder.CreateIndex(
                name: "ix_deviceFirmware_board_source",
                table: "deviceFirmware",
                columns: new[] { "Board", "Source" });

            migrationBuilder.CreateIndex(
                name: "ux_deviceManualOverride_device_relayfunction",
                table: "deviceManualOverride",
                columns: new[] { "DeviceID", "RelayFunction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_deviceScheduleSlot_DeviceConfigControllerID",
                table: "deviceScheduleSlot",
                column: "DeviceConfigControllerID");

            migrationBuilder.CreateIndex(
                name: "ux_deviceType_kit",
                table: "deviceType",
                column: "Kit",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_eventDevice_device_date",
                table: "eventDevice",
                columns: new[] { "DeviceID", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_eventDevice_EventID",
                table: "eventDevice",
                column: "EventID");

            migrationBuilder.CreateIndex(
                name: "IX_eventService_EventID",
                table: "eventService",
                column: "EventID");

            migrationBuilder.CreateIndex(
                name: "IX_eventService_ServiceID",
                table: "eventService",
                column: "ServiceID");

            migrationBuilder.CreateIndex(
                name: "IX_gatewayDeviceMapping_IDDevice",
                table: "gatewayDeviceMapping",
                column: "IDDevice");

            migrationBuilder.CreateIndex(
                name: "ux_gatewayDeviceMapping_gateway_deveui",
                table: "gatewayDeviceMapping",
                columns: new[] { "IDGatewayDevice", "DevEUI" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ruleNotificationState_DeviceFarmUnitZoneID",
                table: "ruleNotificationState",
                column: "DeviceFarmUnitZoneID");

            migrationBuilder.CreateIndex(
                name: "ux_ruleNotificationState_rule_zone",
                table: "ruleNotificationState",
                columns: new[] { "RuleID", "DeviceFarmUnitZoneID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sensorData_device_tenant_date",
                table: "sensorData",
                columns: new[] { "DeviceID", "TenantID", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_sensorData_DeviceFarmUnitID",
                table: "sensorData",
                column: "DeviceFarmUnitID");

            migrationBuilder.CreateIndex(
                name: "ix_sensorData_deviceFarmUnitZone_date",
                table: "sensorData",
                columns: new[] { "DeviceFarmUnitZoneID", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_sensorDataReport_deviceID",
                table: "sensorDataReport",
                column: "deviceID");

            migrationBuilder.CreateIndex(
                name: "IX_simulationSessionDevice_DeviceID",
                table: "simulationSessionDevice",
                column: "DeviceID");

            migrationBuilder.CreateIndex(
                name: "Name_UNIQUE",
                table: "tenant",
                column: "TenantName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenantWifiConfig_tenant",
                table: "tenantWifiConfig",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "ActivationTokenHash_UNIQUE",
                table: "user",
                column: "ActivationTokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "email_UNIQUE",
                table: "user",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_tenant",
                table: "user",
                column: "TenantID");

            migrationBuilder.CreateIndex(
                name: "Username_UNIQUE",
                table: "user",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_userRefreshToken_userID",
                table: "userRefreshToken",
                column: "UserID");

            migrationBuilder.CreateIndex(
                name: "TokenHash_UNIQUE",
                table: "userRefreshToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_userRole_RoleScopeID",
                table: "userRole",
                column: "RoleScopeID");

            migrationBuilder.CreateIndex(
                name: "IX_userUserRole_UserRoleID",
                table: "userUserRole",
                column: "UserRoleID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditLog");

            migrationBuilder.DropTable(
                name: "controllerData");

            migrationBuilder.DropTable(
                name: "deviceCommand");

            migrationBuilder.DropTable(
                name: "deviceConfigControllerRelay");

            migrationBuilder.DropTable(
                name: "deviceDiagnostic");

            migrationBuilder.DropTable(
                name: "deviceDiscoveryReport");

            migrationBuilder.DropTable(
                name: "deviceFirmware");

            migrationBuilder.DropTable(
                name: "deviceManualOverride");

            migrationBuilder.DropTable(
                name: "deviceScheduleSlot");

            migrationBuilder.DropTable(
                name: "deviceSimulation");

            migrationBuilder.DropTable(
                name: "deviceVirtual");

            migrationBuilder.DropTable(
                name: "eventDevice");

            migrationBuilder.DropTable(
                name: "eventService");

            migrationBuilder.DropTable(
                name: "gatewayDeviceMapping");

            migrationBuilder.DropTable(
                name: "ruleNotificationState");

            migrationBuilder.DropTable(
                name: "sensorData");

            migrationBuilder.DropTable(
                name: "sensorDataReport");

            migrationBuilder.DropTable(
                name: "serverConfig");

            migrationBuilder.DropTable(
                name: "simulationSessionDevice");

            migrationBuilder.DropTable(
                name: "tenantWifiConfig");

            migrationBuilder.DropTable(
                name: "userRefreshToken");

            migrationBuilder.DropTable(
                name: "userUserRole");

            migrationBuilder.DropTable(
                name: "deviceTypeRelay");

            migrationBuilder.DropTable(
                name: "eventType");

            migrationBuilder.DropTable(
                name: "deviceFarmUnitZoneRule");

            migrationBuilder.DropTable(
                name: "device");

            migrationBuilder.DropTable(
                name: "simulationSession");

            migrationBuilder.DropTable(
                name: "userRole");

            migrationBuilder.DropTable(
                name: "user");

            migrationBuilder.DropTable(
                name: "deviceFarmUnitZone");

            migrationBuilder.DropTable(
                name: "deviceConfigController");

            migrationBuilder.DropTable(
                name: "deviceConfigSensor");

            migrationBuilder.DropTable(
                name: "deviceRole");

            migrationBuilder.DropTable(
                name: "deviceTypeService");

            migrationBuilder.DropTable(
                name: "deviceType");

            migrationBuilder.DropTable(
                name: "tenant");

            migrationBuilder.DropTable(
                name: "userRoleScope");

            migrationBuilder.DropTable(
                name: "deviceFarmUnit");

            migrationBuilder.DropTable(
                name: "deviceTypeSensor");

            migrationBuilder.DropTable(
                name: "deviceFarm");
        }
    }
}
