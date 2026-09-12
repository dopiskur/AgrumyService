using System.IO.Compression;
using System.Text.Json;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Migration
{
    /// Builds the full portable snapshot of one tenant - see Agrumy.Shared.Models.TenantExport for exactly what is/isn't included and why; read-only, composed from existing IRepository reads.
    public class TenantExportService(ITenantRepository tenantRepo, IUserRepository userRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IDeviceRepository deviceRepo, ISensorDataRepository sensorDataRepo)
    {
        // Human-readable (WriteIndented) - same convention as DeviceFarmUnitZoneRule.ConditionConfig - an admin may open this JSON to sanity-check it before importing elsewhere.
        private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        /// Packages ExportAsync's snapshot into a ZIP (single export.json entry) - same repackaging already applies to the firmware catalog, so a tenant export behaves like every other admin download/upload pair instead of being the one plain-JSON exception.
        public async Task<(Stream Content, string FileName)> BuildExportZipAsync(int tenantId, bool includeSensorData, DateTime? sensorDataSinceUtc, CancellationToken cancellationToken = default)
        {
            TenantExport export = await ExportAsync(tenantId, includeSensorData, sensorDataSinceUtc);

            var zipStream = new MemoryStream();
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                await using Stream entry = zip.CreateEntry(TenantExport.ExportEntryName, CompressionLevel.Optimal).Open();
                await JsonSerializer.SerializeAsync(entry, export, ExportJsonOptions, cancellationToken);
            }
            zipStream.Position = 0;

            string tenantSlug = (export.SourceTenantName ?? "export").ToLowerInvariant().Replace(' ', '-');
            string fileName = $"agrumy-tenant-{tenantSlug}-{DateTime.UtcNow:yyyyMMdd}.zip";
            return (zipStream, fileName);
        }

        public async Task<TenantExport> ExportAsync(int tenantId, bool includeSensorData, DateTime? sensorDataSinceUtc)
        {
            Tenant? tenant = await tenantRepo.TenantGetByIdAsync(tenantId);

            var exportUsers = new List<TenantExportUser>();
            foreach (User u in await userRepo.UsersGetAsync(tenantId))
            {
                if (u.IDUser is not int idUser)
                {
                    continue;
                }
                UserSecret? secret = await userRepo.UserSecretGetAsync(idUser, null, null);
                IReadOnlyList<string> roles = await userRepo.UserRoleNamesGetAsync(idUser);
                exportUsers.Add(new TenantExportUser
                {
                    User = u,
                    PwdHash = secret?.PwdHash,
                    PwdSalt = secret?.PwdSalt,
                    Roles = roles.ToList(),
                });
            }

            IList<DeviceFarmUnit> units = await deviceFarmUnitRepo.DeviceFarmUnitsGetAsync(tenantId);
            var zones = new List<DeviceFarmUnitZone>();
            var rules = new List<DeviceFarmUnitZoneRule>();
            foreach (DeviceFarmUnit unit in units)
            {
                if (unit.IDDeviceFarmUnit is not int idUnit)
                {
                    continue;
                }
                IList<DeviceFarmUnitZone> unitZones = await deviceFarmUnitRepo.DeviceFarmUnitZonesGetAsync(idUnit);
                zones.AddRange(unitZones);
                foreach (DeviceFarmUnitZone zone in unitZones)
                {
                    if (zone.IDDeviceFarmUnitZone is int idZone)
                    {
                        rules.AddRange(await deviceFarmUnitRepo.RulesGetForZoneAsync(idZone));
                    }
                }
            }

            var exportDevices = new List<TenantExportDevice>();
            foreach (Device d in await deviceRepo.DevicesGetAsync(tenantId))
            {
                exportDevices.Add(new TenantExportDevice
                {
                    Device = d,
                    // Read off the in-memory Device, not left to JSON serialization - see TenantExportDevice's remarks for why that would drop them.
                    ApiId = d.ApiId,
                    ApiKey = d.ApiKey,
                    Sensor = d.DeviceConfigSensorID is int sId ? await deviceRepo.DeviceConfigSensorGetAsync(sId) : null,
                    Controller = d.DeviceConfigControllerID is int cId ? await deviceRepo.DeviceConfigControllerGetAsync(cId) : null,
                });
            }

            return new TenantExport
            {
                ExportedAtUtc = DateTime.UtcNow,
                SourceTenantName = tenant?.TenantName,
                Users = exportUsers,
                Units = units,
                Zones = zones,
                ZoneRules = rules,
                Devices = exportDevices,
                IncludesSensorData = includeSensorData,
                SensorData = includeSensorData ? await sensorDataRepo.SensorDataExportGetAsync(tenantId, sensorDataSinceUtc) : null,
            };
        }
    }
}
