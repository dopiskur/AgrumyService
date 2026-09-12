using Agrumy.Dal;
using Agrumy.Shared;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Firmware;
using Agrumy.Api.Quota;
using Agrumy.Api.Security;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Agrumy.Api.Dal
{
    /// IDeviceRepository - device CRUD, configs, fixed type lists, firmware's legacy board-less lookup, diagnostics/fleet, events, and the offline/low-battery alert queries. Needs IServerConfigRepository (hysteresis defaults on add, EventDedupeMinutes, active firmware source) - an already-extracted leaf facet, so no circular dependency.
    internal sealed partial class EfDeviceRepository(AgrumyDbContext db, IOptions<AgrumySettings> settingsOptions, ICache cache, IServerConfigRepository serverConfigRepository, IDeviceOutboxRepository outboxRepository) : IDeviceRepository
    {
        private readonly AgrumySettings settings = settingsOptions.Value;

        public async Task<Device> DeviceAddAsync(Device device, Func<Task<string?>>? quotaCheckAsync = null)
        {
            // Read (and possibly auto-generate) BEFORE QuotaGuard's own transaction below opens - ServerConfigGetAsync's own seed SaveChangesAsync must auto-commit first, so the two never nest.
            ServerConfig serverConfig = await serverConfigRepository.ServerConfigGetAsync(1);

            return await QuotaGuard.RunAsync(db, quotaCheckAsync, async () =>
            {
                var sensorCfg = new DeviceConfigSensorRow();
                var controllerCfg = new DeviceConfigControllerRow
                {
                    // Hysteresis starts at the server-wide default, overridable per device under Device -> Controller - WaterPump limits below follow the same rule.
                    WaterLevelHysteresis = serverConfig.WaterLevelHysteresis,
                    TemperatureHysteresis = serverConfig.TemperatureHysteresis,
                    HumidityHysteresis = serverConfig.HumidityHysteresis,
                    LightHysteresis = serverConfig.LightHysteresis,
                    WaterPumpMaxRunSeconds = serverConfig.WaterPumpMaxRunSeconds,
                    WaterPumpCooldownSeconds = serverConfig.WaterPumpCooldownSeconds,
                };
                db.DeviceConfigSensors.Add(sensorCfg);
                db.DeviceConfigControllers.Add(controllerCfg);
                await db.SaveChangesAsync();

                var row = new DeviceRow
                {
                    TenantID = device.TenantID,
                    DeviceRoleID = device.DeviceRoleID,
                    DeviceFarmUnitID = device.DeviceFarmUnitID,
                    DeviceFarmUnitZoneID = device.DeviceFarmUnitZoneID,
                    DeviceName = device.DeviceName,
                    MacAddress = device.MacAddress,
                    ManualDeviceTypeID = device.ManualDeviceTypeID,
                    ApiId = device.ApiId ?? "",
                    ApiKey = device.ApiKey ?? "",
                    ServicePoint = device.ServicePoint,
                    DeviceTypeServiceID = device.DeviceTypeServiceID,
                    DeviceSensorEnabled = device.DeviceSensorEnabled,
                    DeviceConfigSensorID = sensorCfg.IDDeviceConfigSensor,
                    DeviceControllerEnabled = device.DeviceControllerEnabled,
                    DeviceConfigControllerID = controllerCfg.IDDeviceConfigController,
                    BatteryEnabled = device.BatteryEnabled,
                    Enabled = device.Enabled,
                    ConfigVersion = device.ConfigVersion,
                    IsGateway = device.IsGateway,
                    GatewayProfile = (int?)device.GatewayProfile,
                };
                db.Devices.Add(row);
                await db.SaveChangesAsync();

                // row.IDDevice is populated by SaveChangesAsync above - no need for a caller round-trip Get.
                return ToDto(row);
            });
        }

        // Roadmap #409 - soft delete, replacing the old hard ExecuteDeleteAsync. SensorData/DeviceConfigSensor/DeviceConfigController rows are left untouched (a restore needs its config back exactly as it was); Diagnostics/ControllerData/Simulation are live operational state, not history worth keeping, so those are still hard-deleted - a restored device just rebuilds them on its next config poll.
        public async Task DeviceDeleteAsync(int? idDevice, int? tenantID)
        {
            bool exists = await db.Devices.AsNoTracking().AnyAsync(d => d.IDDevice == idDevice && d.TenantID == tenantID);
            if (!exists)
            {
                return;
            }

            await using var tx = await db.Database.BeginTransactionAsync();
            await db.DeviceDiagnostics.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.ControllerData.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.DeviceSimulations.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.Devices.Where(d => d.IDDevice == idDevice && d.TenantID == tenantID)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Deleted, true).SetProperty(d => d.DeletedAtUtc, DateTimeOffset.UtcNow));
            await tx.CommitAsync();
        }

        /// Every soft-deleted, not-yet-Purged device (roadmap #427 - Purged items move to DevicePendingPurgeGetAsync instead).
        public async Task<IList<Device>> DeviceRecycleBinGetAsync(int? tenantID)
        {
            IQueryable<DeviceRow> q = db.Devices.IgnoreQueryFilters().AsNoTracking().Where(d => d.Deleted && !d.Purged);
            if (tenantID != null)
            {
                q = q.Where(d => d.TenantID == tenantID);
            }
            var rows = await q.OrderByDescending(d => d.DeletedAtUtc).ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        /// Same "no tenant filter, ownership check before an authorized write" role as DeviceGetByIdAsync, but also sees soft-deleted rows (Purged or not) - RecycleBinApiController uses this to resolve a device's owning tenant before calling DeviceRestoreAsync/DeviceRecycleBinMarkPurgedAsync.
        public async Task<Device?> DeviceRecycleBinGetByIdAsync(int idDevice)
        {
            var row = await db.Devices.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(d => d.IDDevice == idDevice && d.Deleted);
            return row == null ? null : ToDto(row);
        }

        /// Roadmap #427 - marked for permanent removal, still restorable until the purge cycle actually reaps it.
        public async Task<IList<Device>> DevicePendingPurgeGetAsync(int? tenantID)
        {
            IQueryable<DeviceRow> q = db.Devices.IgnoreQueryFilters().AsNoTracking().Where(d => d.Deleted && d.Purged);
            if (tenantID != null)
            {
                q = q.Where(d => d.TenantID == tenantID);
            }
            var rows = await q.OrderByDescending(d => d.PurgedAtUtc).ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        /// False if the device doesn't exist, isn't soft-deleted, or belongs to a different tenant - same "caller's tenant must match" rule as DeviceDeleteAsync. Clears Purged too - a device pending permanent removal is still fully recoverable up until the purge cycle actually reaps it.
        public async Task<bool> DeviceRestoreAsync(int idDevice, int? tenantID)
        {
            int updated = await db.Devices.IgnoreQueryFilters()
                .Where(d => d.IDDevice == idDevice && d.TenantID == tenantID && d.Deleted)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Deleted, false).SetProperty(d => d.DeletedAtUtc, (DateTimeOffset?)null)
                    .SetProperty(d => d.Purged, false).SetProperty(d => d.PurgedAtUtc, (DateTimeOffset?)null));
            return updated > 0;
        }

        /// Roadmap #427 - the manual "delete permanently now" trigger; just flips the flag; the actual removal happens later, in DeviceRecycleBinPurgeAsync, once the purge cycle reaps it. False if the device doesn't exist, isn't soft-deleted, or belongs to a different tenant.
        public async Task<bool> DeviceRecycleBinMarkPurgedAsync(int idDevice, int? tenantID)
        {
            int updated = await db.Devices.IgnoreQueryFilters()
                .Where(d => d.IDDevice == idDevice && d.TenantID == tenantID && d.Deleted && !d.Purged)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Purged, true).SetProperty(d => d.PurgedAtUtc, DateTimeOffset.UtcNow));
            return updated > 0;
        }

        /// The scheduled half of marking - evaluated per device's OWNING TENANT (a governing TenantQuota's RecycleBinRetentionDays replaces the tenant's own self-configured override entirely, since #404 moved that field behind the quota; falls back to the tenant's own override, then serverDefaultRetentionDays), since #427 made retention a per-tenant setting. Materializes candidates client-side (recycle-bin volumes are small) rather than trying to push a per-row variable cutoff into a single translatable EF query.
        public async Task<int> DeviceRecycleBinMarkPurgedByRetentionAsync(int serverDefaultRetentionDays, CancellationToken ct)
        {
            DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
            var tenantRetentionDays = await db.Tenants.AsNoTracking().ToDictionaryAsync(t => t.IDTenant!.Value, t => t.RecycleBinRetentionDays, ct);
            var tenantQuotaRetentionDays = await db.TenantQuotas.AsNoTracking().ToDictionaryAsync(q => q.IDTenant, q => q.RecycleBinRetentionDays, ct);

            var candidates = await db.Devices.IgnoreQueryFilters().AsNoTracking()
                .Where(d => d.Deleted && !d.Purged)
                .Select(d => new { d.IDDevice, d.TenantID, d.DeletedAtUtc })
                .ToListAsync(ct);

            var idsToMark = candidates
                .Where(c =>
                {
                    int retentionDays = RecycleBinRetentionResolver.EffectiveRecycleBinRetentionDays(c.TenantID, tenantQuotaRetentionDays, tenantRetentionDays, serverDefaultRetentionDays);
                    return retentionDays > 0 && c.DeletedAtUtc != null && c.DeletedAtUtc <= nowUtc.AddDays(-retentionDays);
                })
                .Select(c => c.IDDevice)
                .ToList();
            if (idsToMark.Count == 0)
            {
                return 0;
            }

            return await db.Devices.IgnoreQueryFilters().Where(d => idsToMark.Contains(d.IDDevice))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.Purged, true).SetProperty(d => d.PurgedAtUtc, nowUtc), ct);
        }

        public async Task<IList<(int IDDevice, int? TenantID)>> DevicePurgedIdsGetAsync()
        {
            var rows = await db.Devices.IgnoreQueryFilters().AsNoTracking().Where(d => d.Deleted && d.Purged)
                .Select(d => new { d.IDDevice, d.TenantID }).ToListAsync();
            return rows.Select(d => (d.IDDevice, d.TenantID)).ToList();
        }

        /// The purge cycle's actual, irreversible removal - every row tied to this device INCLUDING SensorData, matching roadmap #427's "Deleted=1 means ready for purge" decision (SensorData is no longer preserved the way DeviceDeleteAsync's own soft-delete step preserves DeviceConfigSensor/DeviceConfigController for a possible restore - once Purged, there's no restore left to protect). False if the device doesn't exist, isn't Deleted+Purged, or belongs to a different tenant.
        public async Task<bool> DeviceRecycleBinPurgeAsync(int idDevice, int? tenantID)
        {
            DeviceRow? row = await db.Devices.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(d => d.IDDevice == idDevice && d.TenantID == tenantID && d.Deleted && d.Purged);
            if (row is null)
            {
                return false;
            }

            await using var tx = await db.Database.BeginTransactionAsync();
            await PurgeDeviceChildRowsAsync(idDevice);
            await db.Devices.IgnoreQueryFilters().Where(d => d.IDDevice == idDevice).ExecuteDeleteAsync();
            // Device holds these FKs (not the other way around), so they only become safely deletable once the device row referencing them is already gone.
            if (row.DeviceConfigControllerID != null)
            {
                await db.DeviceConfigControllers.Where(c => c.IDDeviceConfigController == row.DeviceConfigControllerID).ExecuteDeleteAsync();
            }
            if (row.DeviceConfigSensorID != null)
            {
                await db.DeviceConfigSensors.Where(c => c.IDDeviceConfigSensor == row.DeviceConfigSensorID).ExecuteDeleteAsync();
            }
            await tx.CommitAsync();
            return true;
        }

        /// Every table with a real, non-cascading FK to Device, including SensorData (roadmap #427 - a Purged device's data goes with it, no orphaning).
        private async Task PurgeDeviceChildRowsAsync(int idDevice)
        {
            await db.SensorData.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.DeviceOutboxItems.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.DeviceManualOverrides.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.DeviceDiscoveryReports.Where(x => x.ScanningDeviceID == idDevice).ExecuteDeleteAsync();
            await db.DeviceDiagnostics.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.DeviceSimulations.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.DeviceVirtuals.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.SimulationSessionDevices.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.ControllerData.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
            await db.GatewayDeviceMappings.Where(x => x.IDDevice == idDevice || x.IDGatewayDevice == idDevice).ExecuteDeleteAsync();
            await db.EventDevices.Where(x => x.DeviceID == idDevice).ExecuteDeleteAsync();
        }

        public async Task<Device?> DeviceGetAsync(int? tenantID, int? idDevice, string? apiId, string? macAddress)
        {
            IQueryable<DeviceRow> q = db.Devices.AsNoTracking().Where(d => d.TenantID == tenantID);

            if (idDevice != null)
            {
                q = q.Where(d => d.IDDevice == idDevice);
            }
            else if (idDevice == null && apiId != null && macAddress == null)
            {
                q = q.Where(d => d.ApiId == apiId);
            }
            else if (idDevice == null && apiId == null && macAddress != null)
            {
                q = q.Where(d => d.MacAddress == macAddress);
            }
            else
            {
                return null; // no lookup key
            }

            var row = await q.FirstOrDefaultAsync();
            return row == null ? null : ToDto(row);
        }

        public async Task<Device?> DeviceGetByIdAsync(int? idDevice)
        {
            var row = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.IDDevice == idDevice);
            return row == null ? null : ToDto(row);
        }

        public async Task<Device?> DeviceGetByApiIdAsync(string? apiId)
        {
            var row = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.ApiId == apiId);
            return row == null ? null : ToDto(row);
        }

        public async Task<IList<Device>> DevicesGetAsync(int? tenantID)
        {
            var rows = await db.Devices.AsNoTracking().Where(d => d.TenantID == tenantID).ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        // Same query minus the tenant filter - callers (DeviceApiController) only reach this after CallerReadsDevicesGlobally passed, mirroring UsersGetAllAsync.
        public async Task<IList<Device>> DevicesGetAllAsync()
        {
            var rows = await db.Devices.AsNoTracking().ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<IList<Device>> DevicesSensorOnlyGetAsync(int? tenantID)
        {
            IQueryable<DeviceRow> devices = db.Devices.AsNoTracking()
                .Where(d => d.DeviceSensorEnabled == true && d.DeviceControllerEnabled != true);
            if (tenantID != null)
            {
                devices = devices.Where(d => d.TenantID == tenantID);
            }
            var rows = await devices.ToListAsync();
            return rows.Select(ToDto).ToList();
        }

        public async Task<bool> DeviceCheckMacAddressAsync(int? tenantID, string? macAddress)
        {
            return await db.Devices.AsNoTracking()
                .AnyAsync(d => d.TenantID == tenantID && d.MacAddress == macAddress);
        }

        public async Task DeviceUpdateAsync(Device? device)
        {
            if (device == null)
            {
                return;
            }

            var row = await db.Devices.FirstOrDefaultAsync(d => d.IDDevice == device.IDDevice);
            if (row == null)
            {
                return;
            }

            // Does not set MacAddress, config-id columns, DeviceFarmUnitID/DeviceFarmUnitZoneID (written exclusively by DeviceAssignToZoneAsync/DeviceUnassignFromZoneAsync/DeviceFarmUnitZoneMigrateAsync to stay consistent), or ApiId/ApiKey (omitting them would wipe a device's real credential).
            row.TenantID = device.TenantID;
            row.DeviceRoleID = device.DeviceRoleID;
            row.DeviceTypeServiceID = device.DeviceTypeServiceID;
            row.DeviceName = device.DeviceName;
            row.ManualDeviceTypeID = device.ManualDeviceTypeID;
            row.ServicePoint = device.ServicePoint;
            row.ServicePublicKey = device.ServicePublicKey;
            row.SleepSeconds = device.SleepSeconds;
            row.SleepDeepEnabled = device.SleepDeepEnabled;
            row.LoRaGatewayEnabled = device.LoRaGatewayEnabled;
            row.DeviceSensorEnabled = device.DeviceSensorEnabled;
            row.DeviceControllerEnabled = device.DeviceControllerEnabled;
            row.BatteryEnabled = device.BatteryEnabled;
            row.Enabled = device.Enabled;
            row.Debug = device.Debug;
            // Only a real DeviceEditForm submission carries these (see DeviceMappingExtensions.ApplyTo) - anything else round-tripping a fetched DeviceDto leaves both null and LocationSource untouched, never blanking a GPS fix.
            if (device.Latitude.HasValue && device.Longitude.HasValue)
            {
                row.Latitude = device.Latitude;
                row.Longitude = device.Longitude;
                row.LocationSource = (int)DeviceLocationSource.Manual;
            }
            // row's own value, not the payload's - the payload can be stale under two concurrent edits, which would otherwise let ConfigVersion regress or collide instead of growing monotonically.
            row.ConfigVersion = (row.ConfigVersion ?? 0) + 1;
            await db.SaveChangesAsync();
            // Dedup via the outbox's own unique (DeviceID, ActiveKey) index - a null return (already-pending ConfigChanged row) is expected, not an error.
            await outboxRepository.AddOutboxItemAsync(row.IDDevice, CommandActionType.ConfigChanged, DateTime.UtcNow, DateTime.UtcNow.AddDays(30));
        }

        public Task DeviceMarkConfigSentAsync(int deviceID, DateTime sentAtUtc) =>
            db.Devices.Where(d => d.IDDevice == deviceID)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastFullConfigSentAt, sentAtUtc));

        // Stores a SHA-256 hash of token, never the raw value - DeviceSessionGetAsync's returned "Token" is
        // therefore also a hash; the caller (DeviceSessionHandler) hashes the incoming token before comparing.
        public Task DeviceSessionSetAsync(int deviceID, string? token, DateTimeOffset? expiresAtUtc) =>
            db.Devices.Where(d => d.IDDevice == deviceID)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.ApiAuthToken, token == null ? null : DeviceAuth.HashSessionToken(token))
                    .SetProperty(d => d.ApiAuthExpiresAtUtc, expiresAtUtc));

        public async Task<(string Token, DateTimeOffset ExpiresAtUtc)?> DeviceSessionGetAsync(string apiId)
        {
            var row = await db.Devices.Where(d => d.ApiId == apiId)
                .Select(d => new { d.ApiAuthToken, d.ApiAuthExpiresAtUtc })
                .FirstOrDefaultAsync();
            return row is { ApiAuthToken: { } token, ApiAuthExpiresAtUtc: { } expiresAtUtc }
                ? (token, expiresAtUtc)
                : null;
        }

        public Task DeviceSensorDetectionResultSetAsync(int deviceID, string? resultJson, DateTimeOffset detectedAt) =>
            db.Devices.Where(d => d.IDDevice == deviceID)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.LastSensorDetectionResult, resultJson)
                    .SetProperty(d => d.LastSensorDetectionAt, detectedAt));

        public async Task<string> DeviceLoRaPrivateKeyGenerateAsync(int deviceID)
        {
            string hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            await db.Devices.Where(d => d.IDDevice == deviceID)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.LoRaPrivateKeyHex, hex));
            // Every existing session was derived from the key just replaced - leaving them would let a session opened under the OLD key keep accepting uplinks encrypted under the new one only by coincidence of a repeated bootNonce (astronomically unlikely, but there is no reason to rely on that).
            await db.DeviceLoRaSessions.Where(s => s.DeviceID == deviceID).ExecuteDeleteAsync();
            return hex;
        }

        public async Task<bool> DeviceLoRaSessionAcceptAsync(int deviceID, byte[] bootNonce, uint counter)
        {
            string bootNonceHex = Convert.ToHexString(bootNonce);

            // Atomic check-and-advance on an already-known session - the WHERE clause IS the replay check, so two gateways forwarding the same frame can't both pass.
            int updated = await db.DeviceLoRaSessions
                .Where(s => s.DeviceID == deviceID && s.BootNonceHex == bootNonceHex && s.MaxCounter < counter)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.MaxCounter, counter)
                    .SetProperty(x => x.LastSeenUtc, DateTimeOffset.UtcNow));
            if (updated > 0)
            {
                return true;
            }
            bool alreadyKnown = await db.DeviceLoRaSessions.AsNoTracking()
                .AnyAsync(s => s.DeviceID == deviceID && s.BootNonceHex == bootNonceHex);
            if (alreadyKnown)
            {
                return false; // known session, counter <= its MaxCounter - replay
            }

            // Unknown bootNonce - a fresh boot. Insert; a unique-PK race against a concurrent first-uplink from the SAME boot is resolved by retrying the check-and-advance above against the row the other request just won.
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var candidate = new DeviceLoRaSessionRow { DeviceID = deviceID, BootNonceHex = bootNonceHex, MaxCounter = counter, FirstSeenUtc = now, LastSeenUtc = now };
            db.DeviceLoRaSessions.Add(candidate);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (DbExceptionClassifier.Classify(ex) == DbFailureKind.ConstraintViolation)
            {
                db.Entry(candidate).State = EntityState.Detached;
                return await DeviceLoRaSessionAcceptAsync(deviceID, bootNonce, counter);
            }

            // Cap at the 32 most-recently-started sessions per device - a crash-looping node re-bootstrapping every few seconds must not grow this table unbounded.
            List<string> keep = await db.DeviceLoRaSessions.AsNoTracking()
                .Where(s => s.DeviceID == deviceID)
                .OrderByDescending(s => s.FirstSeenUtc)
                .Take(32)
                .Select(s => s.BootNonceHex)
                .ToListAsync();
            await db.DeviceLoRaSessions
                .Where(s => s.DeviceID == deviceID && !keep.Contains(s.BootNonceHex))
                .ExecuteDeleteAsync();
            return true;
        }

        public async Task<DeviceLoRaSessionInfo?> DeviceLoRaLatestSessionGetAsync(int deviceID)
        {
            var row = await db.DeviceLoRaSessions.AsNoTracking()
                .Where(s => s.DeviceID == deviceID)
                .OrderByDescending(s => s.LastSeenUtc)
                .FirstOrDefaultAsync();
            return row is null ? null : new DeviceLoRaSessionInfo { BootNonceHex = row.BootNonceHex, MaxCounter = (uint)row.MaxCounter, LastSeenUtc = row.LastSeenUtc };
        }

        /// internal, not private - EfGatewayRepository and EfDeviceFarmUnitRepository also map DeviceRow to Device.
        internal static Device ToDto(DeviceRow d) => new()
        {
            IDDevice = d.IDDevice,
            TenantID = d.TenantID,
            DeviceRoleID = d.DeviceRoleID,
            DeviceFarmUnitID = d.DeviceFarmUnitID,
            DeviceFarmUnitZoneID = d.DeviceFarmUnitZoneID,
            SowingID = d.SowingID,
            FarmParcelZoneID = d.FarmParcelZoneID,
            DeviceConfigSensorID = d.DeviceConfigSensorID,
            DeviceConfigControllerID = d.DeviceConfigControllerID,
            DeviceTypeServiceID = d.DeviceTypeServiceID,
            DeviceName = d.DeviceName,
            MacAddress = d.MacAddress,
            ManualDeviceTypeID = d.ManualDeviceTypeID,
            ApiId = d.ApiId,
            ApiKey = d.ApiKey,
            LoRaPrivateKeyHex = d.LoRaPrivateKeyHex,
            ServicePoint = d.ServicePoint,
            ServicePublicKey = d.ServicePublicKey,
            SleepSeconds = d.SleepSeconds,
            SleepDeepEnabled = d.SleepDeepEnabled,
            LoRaGatewayEnabled = d.LoRaGatewayEnabled,
            DeviceSensorEnabled = d.DeviceSensorEnabled,
            DeviceControllerEnabled = d.DeviceControllerEnabled,
            BatteryEnabled = d.BatteryEnabled,
            Debug = d.Debug,
            FirmwareUpdate = d.FirmwareUpdate,
            FirmwareTargetVersion = d.FirmwareTargetVersion,
            Enabled = d.Enabled,
            ConfigVersion = d.ConfigVersion,
            Latitude = d.Latitude,
            Longitude = d.Longitude,
            LocationSource = (DeviceLocationSource)d.LocationSource,
            DateCreated = d.DateCreated,
            DateModified = d.DateModified,
            IsGateway = d.IsGateway,
            GatewayProfile = d.GatewayProfile is int p ? (GatewayProfile)p : null,
            LastFullConfigSentAt = d.LastFullConfigSentAt,
            LastSensorDetectionResult = d.LastSensorDetectionResult,
            LastSensorDetectionAt = d.LastSensorDetectionAt,
            DeletedAtUtc = d.DeletedAtUtc,
            PurgedAtUtc = d.PurgedAtUtc,
        };
    }
}
