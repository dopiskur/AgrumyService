using System.IO.Compression;
using System.Text.Json;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Migration
{
    /// Builds the full portable snapshot of one organization - see Agrumy.Shared.Models.TenantExport for exactly what is/isn't included and why; read-only, composed from existing IRepository reads.
    public class TenantExportService(
        ITenantRepository tenantRepo, IUserRepository userRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IDeviceRepository deviceRepo, ISensorDataRepository sensorDataRepo,
        IFarmParcelRepository farmParcelRepo, ISowingRepository sowingRepo, ICropCatalogRepository cropCatalogRepo,
        IFieldLogRepository fieldLogRepo, IZonePlantingRepository zonePlantingRepo)
    {
        // Human-readable (WriteIndented) - same convention as DeviceFarmUnitZoneRule.ConditionConfig - an admin may open this JSON to sanity-check it before importing elsewhere.
        private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

        /// Packages ExportAsync's snapshot into a ZIP (single export.json entry) - same repackaging already applies to the firmware catalog, so an organization export behaves like every other admin download/upload pair instead of being the one plain-JSON exception.
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

            var exportParcels = new List<TenantExportFarmParcel>();
            foreach (DeviceFarm farm in (await deviceFarmUnitRepo.DeviceFarmsGetAsync(tenantId)).Where(f => f.FarmType == FarmType.OpenField))
            {
                foreach (FarmParcel parcel in await farmParcelRepo.FarmParcelsGetAsync(farm.IDDeviceFarm!.Value))
                {
                    exportParcels.Add(new TenantExportFarmParcel
                    {
                        Parcel = parcel,
                        Zones = await farmParcelRepo.FarmParcelZonesGetAsync(parcel.IDFarmParcel!.Value),
                    });
                }
            }

            var exportSowings = new List<TenantExportSowing>();
            foreach (Sowing sowing in await sowingRepo.SowingsGetAsync(tenantId))
            {
                Crop? crop = await cropCatalogRepo.CropGetByIdAsync(sowing.CropID);
                exportSowings.Add(new TenantExportSowing { Sowing = sowing, CropName = crop?.Name });
            }

            // Greenhouse's equivalent of Sowing (D8) - one lookup per already-exported zone.
            var exportZonePlantings = new List<ZonePlanting>();
            foreach (DeviceFarmUnitZone zone in zones)
            {
                if (zone.IDDeviceFarmUnitZone is int idZone)
                {
                    exportZonePlantings.AddRange(await zonePlantingRepo.ZonePlantingsGetAsync(idZone));
                }
            }

            // Dnevnik (D6/D7) - one lookup per Sowing/FarmParcelZone/ZonePlanting/DeviceFarmUnitZone, the four mutually-exclusive scopes FieldLogEntriesGetAsync supports.
            var exportFieldLogEntries = new List<TenantExportFieldLogEntry>();
            async Task CollectFieldLogAsync(int? sowingID, int? farmParcelZoneID, int? zonePlantingID, int? deviceFarmUnitZoneID)
            {
                foreach (FieldLogEntry entry in await fieldLogRepo.FieldLogEntriesGetAsync(sowingID, farmParcelZoneID, zonePlantingID, deviceFarmUnitZoneID))
                {
                    IList<FieldLogAttachment> attachments = entry.IDFieldLogEntry is int idEntry ? await fieldLogRepo.FieldLogAttachmentsGetAsync(idEntry) : [];
                    exportFieldLogEntries.Add(new TenantExportFieldLogEntry { Entry = entry, Attachments = attachments });
                }
            }
            foreach (TenantExportSowing es in exportSowings)
            {
                if (es.Sowing.IDSowing is int idSowing)
                {
                    await CollectFieldLogAsync(idSowing, null, null, null);
                }
            }
            foreach (TenantExportFarmParcel ep in exportParcels)
            {
                foreach (FarmParcelZone zone in ep.Zones)
                {
                    if (zone.IDFarmParcelZone is int idZone)
                    {
                        await CollectFieldLogAsync(null, idZone, null, null);
                    }
                }
            }
            foreach (ZonePlanting zp in exportZonePlantings)
            {
                if (zp.IDZonePlanting is int idZonePlanting)
                {
                    await CollectFieldLogAsync(null, null, idZonePlanting, null);
                }
            }
            foreach (DeviceFarmUnitZone zone in zones)
            {
                if (zone.IDDeviceFarmUnitZone is int idZone)
                {
                    await CollectFieldLogAsync(null, null, null, idZone);
                }
            }

            // Urod (D14) - one lookup per Sowing/ZonePlanting, the two scopes HarvestResultsGetAsync supports.
            var exportHarvestResults = new List<HarvestResult>();
            foreach (TenantExportSowing es in exportSowings)
            {
                if (es.Sowing.IDSowing is int idSowing)
                {
                    exportHarvestResults.AddRange(await fieldLogRepo.HarvestResultsGetAsync(idSowing, null));
                }
            }
            foreach (ZonePlanting zp in exportZonePlantings)
            {
                if (zp.IDZonePlanting is int idZonePlanting)
                {
                    exportHarvestResults.AddRange(await fieldLogRepo.HarvestResultsGetAsync(null, idZonePlanting));
                }
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
                FarmParcels = exportParcels,
                Sowings = exportSowings,
                ZonePlantings = exportZonePlantings,
                FieldLogEntries = exportFieldLogEntries,
                HarvestResults = exportHarvestResults,
                IncludesSensorData = includeSensorData,
                SensorData = includeSensorData ? await sensorDataRepo.SensorDataExportGetAsync(tenantId, sensorDataSinceUtc) : null,
            };
        }
    }
}
