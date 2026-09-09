using Agrumy.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Agrumy.Dal
{
    /// Provider-neutral EF Core context (MySQL/MariaDB via Pomelo or PostgreSQL via Npgsql, chosen at runtime) mapped against the legacy camelCase/IDXxx schema - relationship NAVIGATION properties are deliberately not exposed (no .Include()-based traversal), EfRepository does every join in LINQ; the HasOne() calls below configure FK constraints/cascade behavior only, not C# navigation.
    public class AgrumyDbContext : DbContext
    {
        public AgrumyDbContext(DbContextOptions<AgrumyDbContext> options) : base(options) { }

        /// Npgsql materializes timestamptz back into DateTimeOffset using the CLIENT machine's local timezone, not the DB session's (confirmed empirically: SessionTimeZoneInterceptor pins the session to UTC, yet a value written with Offset=0 still read back with the host OS's own offset) - forcing every DateTimeOffset through ToUniversalTime() on both sides makes every stored value provably Offset=0 regardless of host OS timezone, on both providers.
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
        }

        public DbSet<TenantRow> Tenants => Set<TenantRow>();
        public DbSet<TenantWifiConfigRow> TenantWifiConfigs => Set<TenantWifiConfigRow>();
        public DbSet<UserRow> Users => Set<UserRow>();
        public DbSet<RefreshTokenRow> RefreshTokens => Set<RefreshTokenRow>();
        public DbSet<UserRoleRow> UserRoles => Set<UserRoleRow>();
        public DbSet<UserUserRoleRow> UserUserRoles => Set<UserUserRoleRow>();
        public DbSet<UserNotificationPreferenceRow> UserNotificationPreferences => Set<UserNotificationPreferenceRow>();
        public DbSet<ServerConfigRow> ServerConfigs => Set<ServerConfigRow>();

        public DbSet<DeviceRow> Devices => Set<DeviceRow>();
        public DbSet<DeviceFarmRow> DeviceFarms => Set<DeviceFarmRow>();
        public DbSet<DeviceFarmUnitRow> DeviceFarmUnits => Set<DeviceFarmUnitRow>();
        public DbSet<DeviceFarmUnitZoneRow> DeviceFarmUnitZones => Set<DeviceFarmUnitZoneRow>();
        public DbSet<DeviceFarmUnitZoneRuleRow> DeviceFarmUnitZoneRules => Set<DeviceFarmUnitZoneRuleRow>();
        public DbSet<RuleNotificationStateRow> RuleNotificationStates => Set<RuleNotificationStateRow>();
        public DbSet<DeviceRoleRow> DeviceRoles => Set<DeviceRoleRow>();
        public DbSet<DeviceTypeServiceRow> DeviceTypeServices => Set<DeviceTypeServiceRow>();
        public DbSet<DeviceTypeRelayRow> DeviceTypeRelays => Set<DeviceTypeRelayRow>();
        public DbSet<DeviceTypeSensorRow> DeviceTypeSensors => Set<DeviceTypeSensorRow>();
        public DbSet<DeviceTypeRow> DeviceTypes => Set<DeviceTypeRow>();
        public DbSet<DeviceConfigSensorRow> DeviceConfigSensors => Set<DeviceConfigSensorRow>();
        public DbSet<DeviceConfigControllerRow> DeviceConfigControllers => Set<DeviceConfigControllerRow>();
        public DbSet<DeviceConfigControllerRelayRow> DeviceConfigControllerRelays => Set<DeviceConfigControllerRelayRow>();
        public DbSet<DeviceScheduleSlotRow> DeviceScheduleSlots => Set<DeviceScheduleSlotRow>();
        public DbSet<DeviceFirmwareRow> DeviceFirmwares => Set<DeviceFirmwareRow>();
        public DbSet<DeviceDiagnosticRow> DeviceDiagnostics => Set<DeviceDiagnosticRow>();
        public DbSet<DeviceSimulationRow> DeviceSimulations => Set<DeviceSimulationRow>();
        public DbSet<DeviceVirtualRow> DeviceVirtuals => Set<DeviceVirtualRow>();
        public DbSet<SimulationSessionRow> SimulationSessions => Set<SimulationSessionRow>();
        public DbSet<SimulationSessionDeviceRow> SimulationSessionDevices => Set<SimulationSessionDeviceRow>();
        public DbSet<SimulationGroupRow> SimulationGroups => Set<SimulationGroupRow>();
        public DbSet<ExperimentRow> Experiments => Set<ExperimentRow>();
        public DbSet<SensorDataExperimentRow> SensorDataExperiments => Set<SensorDataExperimentRow>();
        public DbSet<ControllerDataExperimentRow> ControllerDataExperiments => Set<ControllerDataExperimentRow>();
        public DbSet<DeviceCommandRow> DeviceCommands => Set<DeviceCommandRow>();
        public DbSet<DeviceManualOverrideRow> DeviceManualOverrides => Set<DeviceManualOverrideRow>();
        public DbSet<GatewayDeviceMappingRow> GatewayDeviceMappings => Set<GatewayDeviceMappingRow>();
        public DbSet<DeviceDiscoveryReportRow> DeviceDiscoveryReports => Set<DeviceDiscoveryReportRow>();

        public DbSet<SensorDataRow> SensorData => Set<SensorDataRow>();
        public DbSet<ControllerDataRow> ControllerData => Set<ControllerDataRow>();
        public DbSet<SensorDataReportRow> SensorDataReports => Set<SensorDataReportRow>();
        public DbSet<EventTypeRow> EventTypes => Set<EventTypeRow>();
        public DbSet<EventDeviceRow> EventDevices => Set<EventDeviceRow>();
        public DbSet<TenantUsageSnapshotRow> TenantUsageSnapshots => Set<TenantUsageSnapshotRow>();

        public DbSet<AuditLogRow> AuditLogs => Set<AuditLogRow>();

        public DbSet<TenantQuotaRow> TenantQuotas => Set<TenantQuotaRow>();
        public DbSet<HorticultureCatalogCropRow> HorticultureCatalogCrops => Set<HorticultureCatalogCropRow>();
        public DbSet<HorticultureCatalogPermaRow> HorticultureCatalogPermas => Set<HorticultureCatalogPermaRow>();
        public DbSet<HorticultureCatalogHydroponicRow> HorticultureCatalogHydroponics => Set<HorticultureCatalogHydroponicRow>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantRow>(e =>
            {
                e.ToTable("tenant");
                e.HasKey(x => x.IDTenant);
                e.Property(x => x.IDTenant).ValueGeneratedOnAdd();
                e.Property(x => x.TenantName).HasMaxLength(100).IsRequired();
                e.Property(x => x.ScheduleTimeZone).HasMaxLength(64); // same cap as serverConfig.ScheduleTimeZone/user.TimeZone
                e.Property(x => x.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.HasIndex(x => x.TenantName).IsUnique().HasDatabaseName("Name_UNIQUE");
            });

            modelBuilder.Entity<TenantQuotaRow>(e =>
            {
                e.ToTable("tenantConfigQuota");
                e.HasKey(x => x.IDTenant);
                e.Property(x => x.IDTenant).ValueGeneratedNever();
                e.HasOne<TenantRow>().WithOne().HasForeignKey<TenantQuotaRow>(x => x.IDTenant).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TenantWifiConfigRow>(e =>
            {
                e.ToTable("tenantConfigWifi");
                e.HasKey(x => x.IDTenantWifiConfig);
                e.Property(x => x.IDTenantWifiConfig).ValueGeneratedOnAdd();
                e.Property(x => x.Ssid).HasMaxLength(32).IsRequired();
                // 512, not 64 (the plaintext WiFi-password cap) - this column now stores SecretProtector.Protect's ciphertext, which for a 63-char WPA2 password already runs ~170+ base64 chars.
                e.Property(x => x.Password).HasMaxLength(512).IsRequired();
                e.Property(x => x.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.HasIndex(x => x.TenantID).HasDatabaseName("ix_tenantConfigWifi_tenant");
            });

            modelBuilder.Entity<UserRoleRow>(e =>
            {
                e.ToTable("userRole");
                e.HasKey(x => x.IDUserRole);
                e.Property(x => x.IDUserRole).ValueGeneratedOnAdd();
                e.Property(x => x.RoleName).HasMaxLength(45);
            });

            modelBuilder.Entity<UserUserRoleRow>(e =>
            {
                e.ToTable("userUserRole");
                e.HasKey(x => new { x.UserID, x.UserRoleID }); // composite - a user cannot hold the same role twice
                e.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.Cascade); // deleting a user drops their role rows
                e.HasOne<UserRoleRow>().WithMany().HasForeignKey(x => x.UserRoleID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<UserRow>(e =>
            {
                e.ToTable("user");
                e.HasKey(x => x.IDUser);
                e.Property(x => x.IDUser).ValueGeneratedOnAdd();
                e.Property(x => x.Email).HasMaxLength(100).IsRequired();
                e.Property(x => x.Username).HasMaxLength(100);
                e.Property(x => x.PwdSalt).HasMaxLength(128); // Nullable - see UserRow.PwdHash for why.
                e.Property(x => x.FirstName).HasMaxLength(100);
                e.Property(x => x.LastName).HasMaxLength(100);
                e.Property(x => x.Phone).HasMaxLength(15);
                e.Property(x => x.DevicePin).HasMaxLength(8); // 6 today; the firmware buffer (char devicePin[8]) caps what a device can echo back at 7 anyway
                e.Property(x => x.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.Property(x => x.DateModified).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.Property(x => x.ActivationTokenHash).HasMaxLength(64); // SHA-256 hex, same shape as userRefreshToken.TokenHash
                e.Property(x => x.TimeZone).HasMaxLength(64); // longest IANA ids are ~30 chars; 64 leaves headroom
                e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("email_UNIQUE");
                e.HasIndex(x => x.Username).IsUnique().HasDatabaseName("Username_UNIQUE");
                e.HasIndex(x => x.ActivationTokenHash).IsUnique().HasDatabaseName("ActivationTokenHash_UNIQUE");
                e.HasIndex(x => x.TenantID).HasDatabaseName("ix_user_tenant"); // Every tenant-scoped user list filters by TenantID alone.
            });

            // Three tables, identical column config - see HorticultureCatalog*Row's own remarks for why they stay separate.
            void ConfigureHorticultureCatalog<TEntity>(string tableName) where TEntity : class
            {
                modelBuilder.Entity<TEntity>(e =>
                {
                    e.ToTable(tableName);
                    e.HasKey("ID");
                    e.Property<int>("ID").ValueGeneratedOnAdd();
                    e.Property<string>("Name").HasMaxLength(100).IsRequired();
                    e.Property<string?>("Description").HasMaxLength(1000);
                });
            }
            ConfigureHorticultureCatalog<HorticultureCatalogCropRow>("horticultureCatalogCrop");
            ConfigureHorticultureCatalog<HorticultureCatalogPermaRow>("horticultureCatalogPerma");
            ConfigureHorticultureCatalog<HorticultureCatalogHydroponicRow>("horticultureCatalogHydroponic");

            modelBuilder.Entity<UserNotificationPreferenceRow>(e =>
            {
                e.ToTable("userNotificationPreference");
                e.HasKey(x => x.IDUserNotificationPreference);
                e.Property(x => x.IDUserNotificationPreference).ValueGeneratedOnAdd();
                e.Property(x => x.Channel).HasMaxLength(20).IsRequired();
                e.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.Cascade); // deleting a user drops their preference rows
                e.HasIndex(x => new { x.UserID, x.EventType, x.Channel }).IsUnique().HasDatabaseName("UserID_EventType_Channel_UNIQUE");
            });

            modelBuilder.Entity<RefreshTokenRow>(e =>
            {
                e.ToTable("userRefreshToken");
                e.HasKey(x => x.IDRefreshToken);
                e.Property(x => x.IDRefreshToken).ValueGeneratedOnAdd();
                e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
                e.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("TokenHash_UNIQUE");
                e.HasIndex(x => x.UserID).HasDatabaseName("ix_userRefreshToken_userID");
                e.HasOne<UserRow>().WithMany().HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ServerConfigRow>(e =>
            {
                e.ToTable("serverConfig");
                e.HasKey(x => x.IDServerConfig);
                e.Property(x => x.IDServerConfig).ValueGeneratedNever();
                e.Property(x => x.FirmwareGitHubRepository).HasMaxLength(200);
                e.Property(x => x.FirmwareCustomRepositoryUrl).HasMaxLength(500);
                // 512, not the 255 a plaintext broker/SMTP password would need - these columns now store SecretProtector.Protect's ciphertext, same reasoning as TenantWifiConfigRow.Password above.
                e.Property(x => x.MqttPassword).HasMaxLength(512);
                e.Property(x => x.EmailPassword).HasMaxLength(512);
                e.Property(x => x.ArchiveHost).HasMaxLength(255);
                e.Property(x => x.ArchiveDatabaseName).HasMaxLength(64); // MySQL's own database-identifier limit
                e.Property(x => x.ArchiveUsername).HasMaxLength(128);
                e.Property(x => x.ArchivePassword).HasMaxLength(512);
            });

            // Unlike DeviceFarmUnit/DeviceFarmUnitZone, Farm has no reserved "0" sentinel row (its optionality on DeviceFarmUnit is expressed via a nullable FK, not a sentinel) - plain AUTO_INCREMENT, no app-side Max+1 dance needed.
            modelBuilder.Entity<DeviceFarmRow>(e =>
            {
                e.ToTable("deviceFarm");
                e.HasKey(x => x.IDDeviceFarm);
                e.Property(x => x.IDDeviceFarm).ValueGeneratedOnAdd();
                e.Property(x => x.DeviceFarmName).HasMaxLength(100);
                e.Property(x => x.Deleted).HasDefaultValue(false);
                e.Property(x => x.Purged).HasDefaultValue(false);
                // Roadmap #409 - every ordinary query sees only live farms; RecycleBinApiController/EfRecycleBinRepository explicitly IgnoreQueryFilters() for the recycle bin listing/restore.
                e.HasQueryFilter(x => !x.Deleted);
            });

            modelBuilder.Entity<DeviceFarmUnitRow>(e =>
            {
                e.ToTable("deviceFarmUnit");
                e.HasKey(x => x.IDDeviceFarmUnit);
                e.Property(x => x.IDDeviceFarmUnit).ValueGeneratedNever();
                e.Property(x => x.DeviceFarmUnitName).HasMaxLength(100);
                e.Property(x => x.Deleted).HasDefaultValue(false);
                e.HasOne<DeviceFarmRow>().WithMany().HasForeignKey(x => x.DeviceFarmID).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
                e.HasQueryFilter(x => !x.Deleted);
            });

            // Real containment FK (Zone -> Unit) - see db/migrations/2026-09-02-deviceunit-zone-containment.sql.
            modelBuilder.Entity<DeviceFarmUnitZoneRow>(e =>
            {
                e.ToTable("deviceFarmUnitZone");
                e.HasKey(x => x.IDDeviceFarmUnitZone);
                e.Property(x => x.IDDeviceFarmUnitZone).ValueGeneratedNever();
                e.Property(x => x.DeviceFarmUnitZoneName).HasMaxLength(120);
                e.Property(x => x.Deleted).HasDefaultValue(false);
                e.HasOne<DeviceFarmUnitRow>().WithMany().HasForeignKey(x => x.DeviceFarmUnitID).OnDelete(DeleteBehavior.NoAction);
                e.HasQueryFilter(x => !x.Deleted);
            });

            // Several rows may share (scope, RelayFunction/SensorMetric); OR semantics across them, and Zone>Unit>Farm>Global precedence, are resolved in EfRepository/RuleHierarchyResolver, not here. Exactly one of DeviceFarmUnitZoneID/DeviceFarmUnitID/DeviceFarmID is set (Global scope: all three null) - enforced in DeviceFarmUnitApiController, not the DB.
            modelBuilder.Entity<DeviceFarmUnitZoneRuleRow>(e =>
            {
                e.ToTable("deviceFarmUnitZoneRule");
                e.HasKey(x => x.IDDeviceFarmUnitZoneRule);
                e.Property(x => x.IDDeviceFarmUnitZoneRule).ValueGeneratedOnAdd();
                e.Property(x => x.Name).HasMaxLength(100).IsRequired();
                e.Property(x => x.RootConditionJson).IsRequired();
                e.HasOne<DeviceFarmUnitZoneRow>().WithMany().HasForeignKey(x => x.DeviceFarmUnitZoneID).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
                e.HasOne<DeviceFarmUnitRow>().WithMany().HasForeignKey(x => x.DeviceFarmUnitID).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
                e.HasOne<DeviceFarmRow>().WithMany().HasForeignKey(x => x.DeviceFarmID).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
                // Cascade (unlike the other three scopes above) - a simulation-scoped rule only ever makes sense alongside the session it was written for, so it goes away with it instead of becoming an orphaned, unreachable row.
                e.HasOne<SimulationSessionRow>().WithMany().HasForeignKey(x => x.SimulationSessionID).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
                // Cascade, same reasoning as SimulationSessionID above - an experiment-scoped rule only makes sense alongside the experiment it was written for.
                e.HasOne<ExperimentRow>().WithMany().HasForeignKey(x => x.ExperimentID).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
                e.HasIndex(x => x.DeviceFarmUnitZoneID).HasDatabaseName("ix_deviceFarmUnitZoneRule_zone");
                e.HasIndex(x => x.DeviceFarmUnitID).HasDatabaseName("ix_deviceFarmUnitZoneRule_unit");
                e.HasIndex(x => x.DeviceFarmID).HasDatabaseName("ix_deviceFarmUnitZoneRule_farm");
                e.HasIndex(x => x.SimulationSessionID).HasDatabaseName("ix_deviceFarmUnitZoneRule_simulationSession");
                e.HasIndex(x => x.ExperimentID).HasDatabaseName("ix_deviceFarmUnitZoneRule_experiment");
                e.HasIndex(x => x.TenantID).HasDatabaseName("ix_deviceFarmUnitZoneRule_tenant");
            });

            modelBuilder.Entity<RuleNotificationStateRow>(e =>
            {
                e.ToTable("ruleNotificationState");
                e.HasKey(x => x.IDRuleNotificationState);
                e.Property(x => x.IDRuleNotificationState).ValueGeneratedOnAdd();
                e.HasOne<DeviceFarmUnitZoneRuleRow>().WithMany().HasForeignKey(x => x.RuleID).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceFarmUnitZoneRow>().WithMany().HasForeignKey(x => x.DeviceFarmUnitZoneID).OnDelete(DeleteBehavior.NoAction);
                e.HasIndex(x => new { x.RuleID, x.DeviceFarmUnitZoneID }).IsUnique().HasDatabaseName("ux_ruleNotificationState_rule_zone");
            });

            modelBuilder.Entity<DeviceRoleRow>(e =>
            {
                e.ToTable("deviceRole");
                e.HasKey(x => x.IDDeviceRole);
                e.Property(x => x.IDDeviceRole).ValueGeneratedNever(); // Fixed catalog; DeviceController.Edit switches on the literal IDs 0/1/2/3, so the seed must control them exactly.
                e.Property(x => x.DeviceRoleName).HasMaxLength(100);
            });

            modelBuilder.Entity<DeviceTypeServiceRow>(e =>
            {
                e.ToTable("deviceTypeService");
                e.HasKey(x => x.IDDeviceTypeService);
                e.Property(x => x.IDDeviceTypeService).ValueGeneratedNever();
                e.Property(x => x.ServiceType).HasMaxLength(5);
            });

            modelBuilder.Entity<DeviceTypeRelayRow>(e =>
            {
                e.ToTable("deviceTypeRelay");
                e.HasKey(x => x.IDDeviceTypeRelay);
                e.Property(x => x.IDDeviceTypeRelay).ValueGeneratedNever();
                e.Property(x => x.RelayName).HasMaxLength(128);
            });

            modelBuilder.Entity<DeviceTypeSensorRow>(e =>
            {
                e.ToTable("deviceTypeSensor");
                e.HasKey(x => x.IDDeviceTypeSensor);
                e.Property(x => x.IDDeviceTypeSensor).ValueGeneratedNever();
                e.Property(x => x.SensorName).HasMaxLength(128);
            });

            modelBuilder.Entity<DeviceTypeRow>(e =>
            {
                e.ToTable("deviceType");
                e.HasKey(x => x.IDDeviceType);
                e.Property(x => x.IDDeviceType).ValueGeneratedOnAdd();
                e.Property(x => x.Kit).HasMaxLength(64).IsRequired();
                // Kit itself is a display-only catalog name now (roadmap #385) - every referencing table has a real numeric FK to IDDeviceType instead. Still unique so auto-registration/dropdowns can look a kit up by name without duplicates.
                e.HasIndex(x => x.Kit).IsUnique().HasDatabaseName("ux_deviceType_kit");
            });

            modelBuilder.Entity<DeviceConfigControllerRow>(e =>
            {
                e.ToTable("deviceConfigController");
                e.HasKey(x => x.IDDeviceConfigController);
                e.Property(x => x.IDDeviceConfigController).ValueGeneratedOnAdd();
            });

            modelBuilder.Entity<DeviceConfigControllerRelayRow>(e =>
            {
                // Replaces the old Relay1-8 columns/FKs (legacy fk_deviceConfigController_relayN) with one row per assigned slot, no fixed ceiling baked into the schema.
                e.ToTable("deviceConfigControllerRelay");
                e.HasKey(x => new { x.IDDeviceConfigController, x.Slot });
                e.HasOne<DeviceConfigControllerRow>().WithMany().HasForeignKey(x => x.IDDeviceConfigController).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<DeviceTypeRelayRow>().WithMany().HasForeignKey(x => x.RelayFunction).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<DeviceConfigSensorRow>(e =>
            {
                e.ToTable("deviceConfigSensor");
                e.HasKey(x => x.IDDeviceConfigSensor);
                e.Property(x => x.IDDeviceConfigSensor).ValueGeneratedOnAdd();
                // Every Sensor* column references deviceTypeSensor (legacy fk_deviceConfigSensor_deviceTypeSensor_*).
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorBattery).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorTemp).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorTempSoil).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorHumid).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorMoist).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorLight).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorCo2).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorTvoc).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorBarometer).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorPH).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorRainLevel).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorWaterLevel).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorWind).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorEc).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeSensorRow>().WithMany().HasForeignKey(x => x.SensorWeight).OnDelete(DeleteBehavior.NoAction);
            });

            // Real one-to-many, replacing the old flat Schedule* columns.
            modelBuilder.Entity<DeviceScheduleSlotRow>(e =>
            {
                e.ToTable("deviceScheduleSlot");
                e.HasKey(x => x.IDDeviceScheduleSlot);
                e.Property(x => x.IDDeviceScheduleSlot).ValueGeneratedOnAdd();
                e.HasOne<DeviceConfigControllerRow>().WithMany().HasForeignKey(x => x.DeviceConfigControllerID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<DeviceRow>(e =>
            {
                e.ToTable("device");
                e.HasKey(x => x.IDDevice);
                e.Property(x => x.IDDevice).ValueGeneratedOnAdd();
                e.Property(x => x.DeviceName).HasMaxLength(128);
                e.Property(x => x.MacAddress).HasMaxLength(64); // AgrumyFirmware sends exactly 12 hex chars; the wider cap is for Agrumy.Gateway's free-form MacAddress uniqueness key.
                e.Property(x => x.FirmwareTargetVersion).HasMaxLength(20); // same cap as deviceFirmware.Version
                e.Property(x => x.ApiId).HasMaxLength(128).IsRequired();
                e.Property(x => x.ApiKey).HasMaxLength(128).IsRequired();
                e.Property(x => x.LoRaPrivateKeyHex).HasMaxLength(64); // AES-256 key, hex-encoded (32 raw bytes)
                e.Property(x => x.ServicePoint).HasMaxLength(200);
                e.Property(x => x.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.Property(x => x.DateModified).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.HasIndex(x => x.ApiId).IsUnique().HasDatabaseName("ApiID_UNIQUE");
                // Neither MySQL/MariaDB nor (for provider-parity, though Postgres could use a real partial index) this codebase's Postgres path support a WHERE clause on CREATE INDEX directly - a generated column that collapses to NULL for a soft-deleted row is the standard cross-provider workaround, since a unique index never treats two NULLs as a collision. Without this, a soft-deleted device still blocks re-registering the same physical MAC until an admin explicitly purges it from the Recycle Bin - the constraint violation surfaces as a raw 500, not "this device is in the Recycle Bin".
                e.Property<string?>("ActiveMacAddress")
                    .HasMaxLength(64)
                    .HasComputedColumnSql(
                        Database.IsNpgsql()
                            ? "(CASE WHEN NOT \"Deleted\" THEN \"MacAddress\" ELSE NULL END)"
                            : "(CASE WHEN `Deleted` = 0 THEN `MacAddress` ELSE NULL END)",
                        stored: true);
                e.HasIndex("ActiveMacAddress", nameof(DeviceRow.TenantID)).IsUnique().HasDatabaseName("ActiveMacAddress_TenantID_UNIQUE"); // Composite, not a bare MacAddress unique - a device can be legitimately resold across tenants.
                e.HasIndex(x => x.TenantID).HasDatabaseName("ix_device_tenant"); // TenantID is the second column of the unique index above, so it can't be used as a prefix for a plain WHERE TenantID = x.
                // Legacy device FKs (fk_device_*). DeviceFarmUnitZoneID has no FK on device.
                e.HasOne<DeviceConfigControllerRow>().WithMany().HasForeignKey(x => x.DeviceConfigControllerID).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceConfigSensorRow>().WithMany().HasForeignKey(x => x.DeviceConfigSensorID).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceRoleRow>().WithMany().HasForeignKey(x => x.DeviceRoleID).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceTypeServiceRow>().WithMany().HasForeignKey(x => x.DeviceTypeServiceID).OnDelete(DeleteBehavior.NoAction);
                // Admin-chosen from the SAME catalog as deviceDiagnostic.DeviceTypeID (no auto-registration needed here - the Web dropdown only ever offers existing catalog entries).
                e.HasOne<DeviceTypeRow>().WithMany().HasForeignKey(x => x.ManualDeviceTypeID).OnDelete(DeleteBehavior.NoAction);
                // Roadmap #406 - IsRequired(false): TenantID is now nullable (genuinely unassigned, distinct from the real TenantID=0 bootstrap tenant).
                e.HasOne<TenantRow>().WithMany().HasForeignKey(x => x.TenantID).OnDelete(DeleteBehavior.NoAction).IsRequired(false);
                e.HasOne<DeviceFarmUnitRow>().WithMany().HasForeignKey(x => x.DeviceFarmUnitID).OnDelete(DeleteBehavior.NoAction);
                e.Property(x => x.Deleted).HasDefaultValue(false);
                e.Property(x => x.Purged).HasDefaultValue(false);
                // Roadmap #409 - every ordinary query (including a real device's own auth/config-poll lookup) sees only live devices; a soft-deleted device is refused exactly like one that never existed. RecycleBinApiController explicitly IgnoreQueryFilters() for the recycle bin.
                e.HasQueryFilter(x => !x.Deleted);
            });

            modelBuilder.Entity<GatewayDeviceMappingRow>(e =>
            {
                e.ToTable("gatewayDeviceMapping");
                e.HasKey(x => x.IDGatewayDeviceMapping);
                e.Property(x => x.IDGatewayDeviceMapping).ValueGeneratedOnAdd();
                e.Property(x => x.DevEUI).HasMaxLength(16).IsRequired();
                e.Property(x => x.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.HasIndex(x => new { x.IDGatewayDevice, x.DevEUI }).IsUnique().HasDatabaseName("ux_gatewayDeviceMapping_gateway_deveui"); // Not unique on IDDevice alone - the same end-device can sit under two gateways (roaming/overlap).
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.IDGatewayDevice).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.IDDevice).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<DeviceCommandRow>(e =>
            {
                e.ToTable("deviceCommand");
                e.HasKey(x => x.IDDeviceCommand);
                e.Property(x => x.IDDeviceCommand).ValueGeneratedOnAdd();
                e.HasIndex(x => new { x.DeviceID, x.Status }).HasDatabaseName("ix_deviceCommand_device_status");
                e.HasIndex(x => new { x.DeviceID, x.ActiveKey }).IsUnique().HasDatabaseName("ux_deviceCommand_device_activekey"); // See DeviceCommandRow.ActiveKey; both providers allow multiple NULLs through a unique index, avoiding MySQL's unsupported partial-index syntax.
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<DeviceManualOverrideRow>(e =>
            {
                e.ToTable("deviceManualOverride");
                e.HasKey(x => x.IDDeviceManualOverride);
                e.Property(x => x.IDDeviceManualOverride).ValueGeneratedOnAdd();
                e.HasIndex(x => new { x.DeviceID, x.RelayFunction }).IsUnique().HasDatabaseName("ux_deviceManualOverride_device_relayfunction"); // Starting a new command for an already-active function replaces it - one row per (device, function).
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<DeviceDiscoveryReportRow>(e =>
            {
                e.ToTable("deviceDiscoveryReport");
                e.HasKey(x => x.IDReport);
                e.Property(x => x.IDReport).ValueGeneratedOnAdd();
                e.Property(x => x.DiscoveredApMac).HasMaxLength(64).IsRequired();
                e.HasIndex(x => x.DiscoveredApMac).HasDatabaseName("ix_deviceDiscoveryReport_apMac");
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.ScanningDeviceID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<DeviceDiagnosticRow>(e =>
            {
                e.ToTable("deviceDiagnostic");
                e.HasKey(x => x.DeviceID);
                e.Property(x => x.DeviceID).ValueGeneratedNever(); // PK is 1:1 with device, deliberately not ValueGeneratedOnAdd.
                e.Property(x => x.FirmwareVersion).HasMaxLength(40); // self-reported, not curated like deviceFirmware.Version - `git describe --dirty` dev builds run e.g. "1.2.3-4-gabc1234-dirty"
                e.Property(x => x.Board).HasMaxLength(40);
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
                // A firmware-reported Kit not yet in the catalog is auto-registered by DeviceDiagnosticUpsertAsync BEFORE this row is written, so the FK never rejects a legitimate device's heartbeat; NULL (never reported / generic build, normalized from "") bypasses the FK entirely.
                e.HasOne<DeviceTypeRow>().WithMany().HasForeignKey(x => x.DeviceTypeID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<DeviceSimulationRow>(e =>
            {
                e.ToTable("deviceSimulation");
                e.HasKey(x => x.DeviceID);
                e.Property(x => x.DeviceID).ValueGeneratedNever(); // 1:1 with device, same PK-is-the-FK shape as DeviceDiagnosticRow above.
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<DeviceVirtualRow>(e =>
            {
                e.ToTable("deviceVirtual");
                e.HasKey(x => x.DeviceID);
                e.Property(x => x.DeviceID).ValueGeneratedNever(); // 1:1 with device, same PK-is-the-FK shape as DeviceDiagnosticRow/DeviceSimulationRow.
                e.Property(x => x.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<SimulationSessionRow>(e =>
            {
                e.ToTable("simulationSession");
                e.HasKey(x => x.IDSimulationSession);
                e.Property(x => x.IDSimulationSession).ValueGeneratedOnAdd();
                e.Property(x => x.Name).HasMaxLength(128);
            });

            modelBuilder.Entity<SimulationSessionDeviceRow>(e =>
            {
                e.ToTable("simulationSessionDevice");
                e.HasKey(x => new { x.IDSimulationSession, x.DeviceID });
                e.HasOne<SimulationSessionRow>().WithMany().HasForeignKey(x => x.IDSimulationSession).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
                // SetNull, not Cascade - deleting the group must not also delete this row's OWN membership record; SimulationGroupDeleteAsync clears the membership itself (turning off the override first), this FK only guards against an orphaned reference if that cleanup is ever skipped.
                e.HasOne<SimulationGroupRow>().WithMany().HasForeignKey(x => x.IDSimulationGroup).OnDelete(DeleteBehavior.SetNull).IsRequired(false);
            });

            modelBuilder.Entity<SimulationGroupRow>(e =>
            {
                e.ToTable("simulationGroup");
                e.HasKey(x => x.IDSimulationGroup);
                e.Property(x => x.IDSimulationGroup).ValueGeneratedOnAdd();
                e.HasOne<SimulationSessionRow>().WithMany().HasForeignKey(x => x.IDSimulationSession).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => x.IDSimulationSession).HasDatabaseName("ix_simulationGroup_session");
            });

            modelBuilder.Entity<ExperimentRow>(e =>
            {
                e.ToTable("experiment");
                e.HasKey(x => x.IDExperiment);
                e.Property(x => x.IDExperiment).ValueGeneratedOnAdd();
                e.Property(x => x.Name).HasMaxLength(128).IsRequired();
                e.HasIndex(x => new { x.TenantID, x.Scope, x.ScopeID }).HasDatabaseName("ix_experiment_tenant_scope"); // ActiveExperimentIdForZoneAsync/ActiveExperimentIdsByZoneAsync's own lookup shape.
            });

            modelBuilder.Entity<SensorDataExperimentRow>(e =>
            {
                e.ToTable("dataSensorExperiment");
                e.HasKey(x => x.IDSensorDataExperiment);
                e.Property(x => x.IDSensorDataExperiment).ValueGeneratedOnAdd();
                e.HasOne<ExperimentRow>().WithMany().HasForeignKey(x => x.IDExperiment).OnDelete(DeleteBehavior.NoAction); // NoAction, not Cascade - kept permanently even if the experiment row itself is ever removed.
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
                e.HasIndex(x => new { x.IDExperiment, x.DateCreated }).HasDatabaseName("ix_dataSensorExperiment_experiment_date");
            });

            modelBuilder.Entity<ControllerDataExperimentRow>(e =>
            {
                e.ToTable("dataControllerExperiment");
                e.HasKey(x => x.IDControllerDataExperiment);
                e.Property(x => x.IDControllerDataExperiment).ValueGeneratedOnAdd();
                e.HasOne<ExperimentRow>().WithMany().HasForeignKey(x => x.IDExperiment).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
                e.HasIndex(x => new { x.IDExperiment, x.DateCreated }).HasDatabaseName("ix_dataControllerExperiment_experiment_date");
            });

            modelBuilder.Entity<DeviceFirmwareRow>(e =>
            {
                e.ToTable("deviceFirmware");
                e.HasKey(x => x.IDDeviceFirmware);
                e.Property(x => x.IDDeviceFirmware).ValueGeneratedOnAdd();
                e.Property(x => x.Version).HasMaxLength(20);
                e.Property(x => x.Board).HasMaxLength(40);
                e.Property(x => x.FileName).HasMaxLength(120);
                e.Property(x => x.Sha256).HasMaxLength(64);
                e.Property(x => x.FullImageFileName).HasMaxLength(120); // Same cap as the OTA FileName above - full-image names are only a few characters longer.
                e.Property(x => x.FullImageSha256).HasMaxLength(64);
                e.Property(x => x.DateAdded).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.HasIndex(x => new { x.Board, x.Source }).HasDatabaseName("ix_deviceFirmware_board_source"); // The "which .bin for this board" lookups all filter on (Board, Source).
            });

            modelBuilder.Entity<SensorDataRow>(e =>
            {
                e.ToTable("dataSensor");
                e.HasKey(x => x.IDSensorData);
                e.Property(x => x.IDSensorData).ValueGeneratedOnAdd();
                // Legacy Battery/Moisture/WaterLevel are tinyint(1); the DTO exposes them as int, so a fresh DB uses int (old tinyint(1) columns still read fine).
                e.HasIndex(x => new { x.DeviceID, x.TenantID, x.DateCreated })
                 .HasDatabaseName("ix_dataSensor_device_tenant_date");
                e.HasIndex(x => new { x.DeviceFarmUnitZoneID, x.DateCreated })
                 .HasDatabaseName("ix_dataSensor_deviceFarmUnitZone_date"); // The 24h trend sparkline query filters directly by zone, not by device.
                // Legacy fk_sensorData_* (no FK on dataSensor.TenantID).
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceFarmUnitRow>().WithMany().HasForeignKey(x => x.DeviceFarmUnitID).OnDelete(DeleteBehavior.NoAction);
                e.HasOne<DeviceFarmUnitZoneRow>().WithMany().HasForeignKey(x => x.DeviceFarmUnitZoneID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<ControllerDataRow>(e =>
            {
                e.ToTable("dataController");
                e.HasKey(x => x.IDControllerData);
                e.Property(x => x.IDControllerData).ValueGeneratedOnAdd();
                e.HasIndex(x => new { x.DeviceID, x.RelayFunction }).IsUnique().HasDatabaseName("ux_dataController_device_relayFunction");
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<SensorDataReportRow>(e =>
            {
                e.ToTable("sensorDataReport");
                e.HasKey(x => x.IDSensorDataReport);
                e.Property(x => x.IDSensorDataReport).ValueGeneratedOnAdd();
                e.Property(x => x.DeviceID).HasColumnName("deviceID");
                e.Property(x => x.ReportName).HasMaxLength(128);
                e.Property(x => x.DateGenerated).HasDefaultValueSql("CURRENT_TIMESTAMP");
                e.Property(x => x.SensorData).HasColumnName("sensorData");
                e.HasOne<DeviceRow>().WithMany().HasForeignKey(x => x.DeviceID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<EventTypeRow>(e =>
            {
                e.ToTable("eventType");
                e.HasKey(x => x.IDEventType);
                e.Property(x => x.IDEventType).ValueGeneratedNever(); // Fixed catalog, mirrors Agrumy.Shared.Models.DeviceEventType's own numeric values.
                e.Property(x => x.EventTypeName).HasMaxLength(64);
            });

            modelBuilder.Entity<EventDeviceRow>(e =>
            {
                e.ToTable("eventDevice");
                e.HasKey(x => x.IDEventDevice);
                e.Property(x => x.IDEventDevice).ValueGeneratedOnAdd();
                e.HasIndex(x => new { x.DeviceID, x.Date }).HasDatabaseName("ix_eventDevice_device_date"); // Every device-events read/problem-alert scan filters DeviceID plus a Date range.
                e.HasOne<EventTypeRow>().WithMany().HasForeignKey(x => x.EventID).OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<AuditLogRow>(e =>
            {
                e.ToTable("auditLog");
                e.HasKey(x => x.IDAuditLog);
                e.Property(x => x.IDAuditLog).ValueGeneratedOnAdd();
                e.Property(x => x.ActorEmail).HasMaxLength(255);
                e.Property(x => x.Action).HasMaxLength(100);
                e.Property(x => x.TargetType).HasMaxLength(50);
                e.Property(x => x.TargetId).HasMaxLength(50);
                e.HasIndex(x => new { x.TenantID, x.TimestampUtc }).HasDatabaseName("ix_auditLog_tenant_timestamp"); // A Global admin's cross-tenant listing scans the whole table, acceptable at this volume.
            });

            modelBuilder.Entity<TenantUsageSnapshotRow>(e =>
            {
                e.ToTable("tenantUsageSnapshot");
                e.HasKey(x => x.IDTenantUsageSnapshot);
                e.Property(x => x.IDTenantUsageSnapshot).ValueGeneratedOnAdd();
                e.HasOne<TenantRow>().WithMany().HasForeignKey(x => x.TenantID).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.TenantID, x.SnapshotDateUtc }).IsUnique().HasDatabaseName("ix_tenantUsageSnapshot_tenant_date");
            });
        }
    }
}
