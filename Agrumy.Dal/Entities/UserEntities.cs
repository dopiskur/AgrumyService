namespace Agrumy.Dal.Entities
{
    // Persistence entities mapped 1:1 to table columns, kept separate from the Agrumy.Shared.Models DTOs (flattened joins, MVC attributes) - EfRepository projects these onto the DTOs.

    public class TenantRow
    {
        // Nullable, not int - EF Core's ValueGeneratedOnAdd only sends an explicit value in the INSERT when the property differs from its CLR type's default; for `int` that default is 0, exactly the literal value SeedDefaultTenantAsync needs to insert for the default tenant. Nullable flips the "not set yet" sentinel to null instead, so IDTenant=0 is treated as a real, explicit value and actually reaches the INSERT statement.
        public int? IDTenant { get; set; }
        public string TenantName { get; set; } = "";
        public string? ScheduleTimeZone { get; set; } // See Agrumy.Shared.Models.Tenant.ScheduleTimeZone.
        public double? Latitude { get; set; } // See Agrumy.Shared.Models.Tenant.Latitude.
        public double? Longitude { get; set; } // See Agrumy.Shared.Models.Tenant.Longitude.
        public bool EmergencyStopActive { get; set; } // See Agrumy.Shared.Models.Tenant.EmergencyStopActive.
        public DateTimeOffset? DateCreated { get; set; }

        // Roadmap #427 - per-tenant override, same "null falls back to ServerConfig's server-wide default" convention as ScheduleTimeZone/Latitude/Longitude above.
        public int? RecycleBinRetentionDays { get; set; }

        // Roadmap #509 - per-tenant alert override, same cascade convention as RecycleBinRetentionDays above; see Agrumy.Shared.Models.TenantAlertConfig.
        public bool? ProblemEventAlertsEnabled { get; set; }
        public int? ProblemEventExpiryHours { get; set; }
        public double? BatteryLowThreshold { get; set; }
        public double? BatteryLowHysteresis { get; set; }
        public double? TankRefillThreshold { get; set; }
        public double? TankRefillHysteresis { get; set; }
        public int? EventDedupeMinutes { get; set; }
    }

    /// See Agrumy.Shared.Models.TenantQuota - IDTenant is both PK and FK (1:1 with TenantRow), absent row means "not yet explicitly configured" (EfTenantQuotaRepository falls back to TenantQuota.Default, never unlimited).
    public class TenantQuotaRow
    {
        public int IDTenant { get; set; }
        public int MaxDevices { get; set; }
        public int MaxFarms { get; set; }
        public int MaxUnits { get; set; }
        public int MaxZones { get; set; }
        public int MaxCrops { get; set; }
        public int MaxParcels { get; set; }
        public int MaxControllersPerDevice { get; set; }
        public int MaxSensorsPerDevice { get; set; }
        public bool MqttEnabled { get; set; }
        public bool LoRaEnabled { get; set; }
        public bool GatewayEnabled { get; set; }
        public int MinSensorIntervalMinutes { get; set; }
        public int MaxDataRetentionDays { get; set; }
        public int RecycleBinRetentionDays { get; set; }
        public int MaxUsers { get; set; }
        public int MaxSimulations { get; set; }
    }

    /// See Agrumy.Shared.Models.UserNotificationPreference - absence of a row for a given (UserID, EventType, Channel) means enabled, so this table only ever holds explicit opt-outs.
    public class UserNotificationPreferenceRow
    {
        public int IDUserNotificationPreference { get; set; }
        public int UserID { get; set; }
        public int EventType { get; set; }
        public string Channel { get; set; } = "";
        public bool Enabled { get; set; }
    }

    /// See Agrumy.Shared.Models.TenantWeatherState - one row per tenant, upserted by WeatherEvaluator/FrostAlertEvaluator.
    public class TenantWeatherStateRow
    {
        public int TenantID { get; set; }
        public bool WeatherRainPredicted { get; set; }
        public DateTimeOffset? WeatherCheckedAtUtc { get; set; }
        public bool FrostPredicted { get; set; }
        public int? FrostPredictedHoursAhead { get; set; }
        public DateTimeOffset? FrostCheckedAtUtc { get; set; }
    }

    /// See Agrumy.Shared.Models.TenantWifiConfig.
    public class TenantWifiConfigRow
    {
        public int IDTenantWifiConfig { get; set; }
        public int TenantID { get; set; }
        public string Ssid { get; set; } = "";
        public string Password { get; set; } = "";
        public DateTimeOffset? DateCreated { get; set; }
    }

    public class UserRoleRow
    {
        public int IDUserRole { get; set; }
        public string? RoleName { get; set; }
    }

    /// A user can hold several roles at once - this many-to-many junction is the sole source of truth for authorization.
    public class UserUserRoleRow
    {
        public int UserID { get; set; }
        public int UserRoleID { get; set; }
    }

    public class UserRow
    {
        public int IDUser { get; set; }
        // Nullable so a deleted tenant can leave its surviving users "Unassigned" instead of forcing them onto tenant 0 - only EfTenantRepository.TenantDeleteAsync ever writes null here.
        public int? TenantID { get; set; }
        public string Email { get; set; } = "";
        public string? Username { get; set; }
        public string? PwdHash { get; set; } // Null only on the bootstrap seed row; AuthenticationProvider.VerifyHash rejects a null hash so it can't authenticate until BootstrapAdminSetPasswordAsync runs.
        public string? PwdSalt { get; set; }
        public string? BootstrapSecretHash { get; set; } // One-time bootstrap setup secret, set only on the seed row and cleared on success.
        public string? BootstrapSecretSalt { get; set; }
        public string? DevicePin { get; set; } // 6-char generated code; null = never issued or explicitly cleared.
        public DateTimeOffset? DevicePinExpires { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public bool? PhoneEnabled { get; set; }
        public bool? Enabled { get; set; }
        public DateTimeOffset? DateCreated { get; set; }
        public DateTimeOffset? DateModified { get; set; }

        public bool EmailVerified { get; set; } // Independent gate on top of Enabled, proving email ownership.
        public string? ActivationTokenHash { get; set; }
        public DateTimeOffset? ActivationTokenExpiresAt { get; set; }

        public DateTimeOffset? ActivationLastSentAt { get; set; } // Resend-cooldown bookkeeping only, never surfaced on the public User DTO.

        public string? TimeZone { get; set; } // IANA zone id, not a raw UTC offset, so TimeZoneInfo resolves it correctly across DST; null = presented as UTC (see Agrumy.Shared.Utils.TimeZoneHelper).

        public int UIMode { get; set; } // See Agrumy.Shared.Models.UIMode.

        public bool MustChangePassword { get; set; } // See Agrumy.Shared.Models.User.MustChangePassword.

        public DateTimeOffset? TokensValidAfterUtc { get; set; } // See Agrumy.Shared.Models.User.TokensValidAfterUtc.
    }

    /// One issued JWT refresh token - single-use, a rotation marks the row revoked and points ReplacedByTokenHash at its successor so a reused token is detectable; only the hash is stored.
    public class RefreshTokenRow
    {
        public int IDRefreshToken { get; set; }
        public int UserID { get; set; }
        public string TokenHash { get; set; } = "";
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
        public string? ReplacedByTokenHash { get; set; }
    }

    public class ServerConfigRow
    {
        public int IDServerConfig { get; set; }
        public string? JWTKey { get; set; }
        public int? PortHTTP { get; set; }
        public int? PortHTTPS { get; set; }

        // Server-wide hysteresis defaults - see Agrumy.Shared.Models.ServerConfig for the full story.
        public double? WaterLevelHysteresis { get; set; }
        public double? TemperatureHysteresis { get; set; }
        public double? HumidityHysteresis { get; set; }
        public double? LightHysteresis { get; set; }

        public double? BatteryLowThreshold { get; set; }
        public double? BatteryLowHysteresis { get; set; }
        public double? TankRefillThreshold { get; set; }
        public double? TankRefillHysteresis { get; set; }
        public int? WaterPumpMaxRunSeconds { get; set; }
        public int? WaterPumpCooldownSeconds { get; set; }
        public int? EventDedupeMinutes { get; set; }
        public int? ActivationResendCooldownMinutes { get; set; }
        public int? MaxRulesPerZone { get; set; }
        public bool AllowSelfServiceTenantCreation { get; set; }
        public bool TenantManagementEnabled { get; set; }
        // ScheduleTimeZone is now per-tenant (see TenantRow.ScheduleTimeZone) - the serverConfig column is left in place, unmapped, rather than dropped.
        public int FirmwareSource { get; set; }
        public string? FirmwareGitHubRepository { get; set; }
        public string? FirmwareCustomRepositoryUrl { get; set; }
        public int? FirmwareRefreshIntervalHours { get; set; }
        public DateTimeOffset? FirmwareLastRefreshedAtUtc { get; set; }
        public int? SensorDataRetentionDays { get; set; }

        // Roadmap #409 - how long a soft-deleted Farm/Device stays restorable from the Recycle Bin, 0-90; default 30 applied where the column reads NULL (fresh row, or a pre-#409 install).
        public int? RecycleBinRetentionDays { get; set; }
        public int? SatelliteRasterRetentionDays { get; set; }
        // Roadmap #409 - PurgeOrphanedSensorDataBackgroundService only runs when this is true (manual "Purge orphaned sensor data" trigger is always available regardless).
        public bool PurgeOrphanedSensorDataScheduleEnabled { get; set; }

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public double? WeatherLocationLat { get; set; }
        public double? WeatherLocationLon { get; set; }
        public int? WeatherPollIntervalMinutes { get; set; }
        public double? WeatherRainSkipThreshold { get; set; }

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public int? FrostLookaheadHours { get; set; }
        public double? FrostTempThresholdC { get; set; }
        public double? FrostCloudinessMaxPercent { get; set; }
        public double? FrostWindMaxMetersPerSecond { get; set; }

        // See Agrumy.Shared.Models.ServerConfig's own copy of this for the full explanation.
        public bool ODataEnabled { get; set; }

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public bool GatewayEnabled { get; set; }
        public int GatewayMode { get; set; }
        public int GatewayWaitWindowSeconds { get; set; } = 30;

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public bool ProblemEventAlertsEnabled { get; set; } = true;
        public int ProblemEventExpiryHours { get; set; } = 24;

        // See Agrumy.Shared.Security.PasswordPolicy for the full explanation.
        public int PasswordMinLength { get; set; }
        public bool PasswordRequireComplexity { get; set; }

        // See Agrumy.Shared.Models.ServerConfig's own copy for the full explanation.
        public int ConfigHeartbeatHours { get; set; } = 1;

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public bool MqttTransportEnabled { get; set; }
        public string? MqttBrokerHost { get; set; }
        public int MqttBrokerPort { get; set; } = 1883;
        public string? MqttUsername { get; set; }
        public string? MqttPassword { get; set; }
        public DateTimeOffset? MqttCredentialsSyncedAtUtc { get; set; }

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public bool EmailEnabled { get; set; }
        public string? EmailHost { get; set; }
        public int EmailPort { get; set; } = 587;
        public bool EmailUseStartTls { get; set; } = true;
        public string? EmailUsername { get; set; }
        public string? EmailPassword { get; set; }
        public string? EmailFromAddress { get; set; }
        public string EmailFromName { get; set; } = "Agrumy";

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public bool WebhookEnabled { get; set; }
        public string? WebhookUrl { get; set; }
        public string? WebhookSecret { get; set; }

        public int DevicePinValidMinutes { get; set; } = 60;

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public bool ArchiveEnabled { get; set; }
        public int ArchiveCutoffMode { get; set; }
        public DateOnly? ArchiveCustomCutoffDate { get; set; }
        public int? ArchiveCustomRollingDays { get; set; }
        public string? ArchiveHost { get; set; }
        public int? ArchivePort { get; set; } = 3306;
        public string? ArchiveDatabaseName { get; set; }
        public string? ArchiveUsername { get; set; }
        public string? ArchivePassword { get; set; }
        public DateTimeOffset? ArchiveLastRunAtUtc { get; set; }

        // See Agrumy.Shared.Models.ServerConfig's own copies of these for the full explanation.
        public bool ArkodGeoPackageSyncEnabled { get; set; }
        public DateTimeOffset? ArkodGeoPackageSyncedAtUtc { get; set; }
    }
}
