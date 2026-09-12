namespace Agrumy.Shared.Models
{
    /// Where an import lands: ByName matches/creates an organization by exact name; AsSentinel targets TenantID=0 and is only reachable via TenantApiController.ImportAsSentinel while the bootstrap Global Admin is still unclaimed (ITenantRepository.TenantZeroIsEmptyAsync).
    public enum TenantImportTarget
    {
        ByName = 0,
        AsSentinel = 1,
    }

    /// One exported user: the User DTO plus its portable PBKDF2 hash/salt and role NAMES (not the install-specific userRole.IDUserRole, which would need remapping).
    public class TenantExportUser
    {
        public User User { get; set; } = new();
        public string? PwdHash { get; set; }
        public string? PwdSalt { get; set; }
        public IList<string> Roles { get; set; } = [];
    }

    /// One exported device: Device+Sensor/Controller (DeviceUpdate's grouping), with ApiId/ApiKey carried as separate fields since both are [JsonIgnore] on Device and would otherwise silently drop from the export.
    public class TenantExportDevice
    {
        public Device Device { get; set; } = new();
        public string? ApiId { get; set; }
        public string? ApiKey { get; set; }
        public DeviceConfigSensor? Sensor { get; set; }
        public DeviceConfigController? Controller { get; set; }
    }

    /// One exported Open-Field parcel with its zones (Detaljni dizajn R, D2/D3) - every source-tenant FarmParcel lands under ONE freshly-created Open-Field farm on import (see TenantImportService), the same "collapse into one farm" simplification EnsureFirstFarmAsync already applies to Greenhouse Units.
    public class TenantExportFarmParcel
    {
        public FarmParcel Parcel { get; set; } = new();
        public IList<FarmParcelZone> Zones { get; set; } = [];
    }

    /// One exported Sowing (Detaljni dizajn R, D9/D11) - CropName is resolved at export time so import can find-or-create the equivalent Crop by name on the target (same approach the live "New sowing" form already uses), no Crop id remapping needed.
    public class TenantExportSowing
    {
        public Sowing Sowing { get; set; } = new();
        public string? CropName { get; set; }
    }

    /// One exported dnevnik entry with its attachments (Detaljni dizajn R, D6/D7) - FieldLogAttachment.StoragePath is exported as metadata only; the physical file under fieldlog-store on the SOURCE server does not travel with the export (no export in this codebase moves binary files - firmware/satellite rasters don't either).
    public class TenantExportFieldLogEntry
    {
        public FieldLogEntry Entry { get; set; } = new();
        public IList<FieldLogAttachment> Attachments { get; set; } = [];
    }

    /// The full portable snapshot of one organization (excludes install-wide ServerConfig/firmware catalog, includes SensorData only when opt-in) - SENSITIVE (password hashes, device ApiKeys), never persisted server-side, streamed directly to the admin's browser.
    public class TenantExport
    {
        public const string CurrentFormatVersion = "1";
        // The single entry name inside the ZIP TenantExportService.BuildExportZipAsync produces - shared here since Agrumy.Web (no reference to Agrumy.Api) also needs it to unpack an upload.
        public const string ExportEntryName = "export.json";
        public string FormatVersion { get; set; } = CurrentFormatVersion;
        public DateTimeOffset ExportedAtUtc { get; set; }
        public string? SourceTenantName { get; set; }

        public IList<TenantExportUser> Users { get; set; } = [];
        public IList<DeviceFarmUnit> Units { get; set; } = [];
        public IList<DeviceFarmUnitZone> Zones { get; set; } = [];
        public IList<DeviceFarmUnitZoneRule> ZoneRules { get; set; } = [];
        public IList<TenantExportDevice> Devices { get; set; } = [];

        // ---- Open-Field / Greenhouse growing-cycle layer (Detaljni dizajn R) - #585 ----
        public IList<TenantExportFarmParcel> FarmParcels { get; set; } = [];
        public IList<TenantExportSowing> Sowings { get; set; } = [];
        /// Greenhouse's equivalent of Sowing (D8) - DeviceFarmUnitZoneID is an OLD/source id, remapped on import through the same map Zones/ZoneRules already use.
        public IList<ZonePlanting> ZonePlantings { get; set; } = [];
        public IList<TenantExportFieldLogEntry> FieldLogEntries { get; set; } = [];
        public IList<HarvestResult> HarvestResults { get; set; } = [];

        public bool IncludesSensorData { get; set; }
        public IList<SensorData>? SensorData { get; set; }
    }

    /// Body of POST /api/Tenant/Import (ByName only - ImportAsSentinel takes a bare TenantExport, no target name needed).
    public class TenantImportRequest
    {
        public TenantExport? Export { get; set; }
        /// Required for ByName - matched case-sensitively against an existing organization, or used to create a new one if none matches.
        public string? TargetTenantName { get; set; }
    }

    /// What actually happened - counts, not the imported rows themselves (the caller already has those, in the export they just submitted).
    public class TenantImportResult
    {
        public int TargetTenantId { get; set; }
        public string? TargetTenantName { get; set; }
        public int UsersImported { get; set; }
        public int UsersSkipped { get; set; }
        public int DevicesSkipped { get; set; }
        /// Human-readable reason for each skipped user/device (unique-constraint conflicts with something already on the target).
        public IList<string> SkippedReasons { get; set; } = [];
        public int DevicesImported { get; set; }
        public int UnitsImported { get; set; }
        public int ZonesImported { get; set; }
        public int ZoneRulesImported { get; set; }
        public int SensorDataRowsImported { get; set; }
        public int FarmParcelsImported { get; set; }
        public int FarmParcelZonesImported { get; set; }
        public int SowingsImported { get; set; }
        public int ZonePlantingsImported { get; set; }
        public int FieldLogEntriesImported { get; set; }
        public int FieldLogAttachmentsImported { get; set; }
        public int HarvestResultsImported { get; set; }
    }
}
