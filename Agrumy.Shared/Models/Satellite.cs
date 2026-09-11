namespace Agrumy.Shared.Models
{
    public enum SatelliteProvider
    {
        CdseSentinelHub = 1,
        SentinelHubCommercial = 2,
    }

    public enum SatellitePlanTier
    {
        Free = 1,
        Paid = 2,
    }

    public enum SatelliteIndex
    {
        Ndvi = 1,
        Ndmi = 2,
        Ndwi = 3,
        Ndsi = 4,
        SwirComposite = 5,
        NaturalColor = 6,
    }

    /// S-B2 - which imagery source a tenant reads from, all through the same CdseSentinelHubSource provider (D2); only PlanTier=Paid unlocks anything but Sentinel2.
    public enum SatelliteCollection
    {
        Sentinel2 = 1,
        PlanetScope = 2,
        PleiadesSpot = 3,
    }

    /// Per-tenant satellite module config (D1/D8) - a tenant with no row (or Enabled=false) has no module at all. ClientSecret is write-only on the wire: PUT with it blank keeps whatever is already stored, GET never echoes the real value back (HasSecret tells the UI whether one is configured).
    public class TenantSatelliteConfig
    {
        public int IDTenant { get; set; }
        public SatelliteProvider Provider { get; set; } = SatelliteProvider.CdseSentinelHub;
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public bool HasSecret { get; set; }
        public SatellitePlanTier PlanTier { get; set; } = SatellitePlanTier.Free;
        /// S-B2 - only selectable when PlanTier=Paid; Free silently stays Sentinel2 regardless of what's stored (the API enforces this on write, not just the UI).
        public SatelliteCollection Collection { get; set; } = SatelliteCollection.Sentinel2;
        /// S-B2 - the tenant's own Sentinel Hub BYOC collection id for PlanetScope/Pleiades (each Paid tenant subscribes to their own commercial collection; Sentinel2 needs none).
        public string? CommercialCollectionId { get; set; }
        public List<SatelliteIndex> DefaultIndices { get; set; } = [];
        public int MaxCloudPercent { get; set; } = 40;
        public int MinValidPixelPercent { get; set; } = 70;
        public bool Enabled { get; set; }
        public DateTimeOffset? LastTokenIssuedUtc { get; set; }
        public string? LastQuotaSnapshotJson { get; set; }
        public DateTimeOffset? QuotaPausedUntilUtc { get; set; }
        /// D11 - null falls back to ServerConfig.SatelliteRasterRetentionDays, same per-tenant-override cascade as Tenant.RecycleBinRetentionDays.
        public int? RasterRetentionDaysOverride { get; set; }
        /// Dedup for the SatelliteQuotaPaused notification - cleared once QuotaPausedUntilUtc passes, same "notified until it clears" shape as DeviceRow.OfflineNotifiedAt.
        public DateTimeOffset? QuotaPausedNotifiedAtUtc { get; set; }
    }

    /// One ingested scene for one zone (D9) - UNIQUE(FarmParcelZoneID, SourceSceneId) makes the daily job idempotent against re-processing the same scene.
    public class FarmParcelZoneSatelliteScene
    {
        public int IDFarmParcelZoneSatelliteScene { get; set; }
        public int FarmParcelZoneID { get; set; }
        public DateOnly SceneDateUtc { get; set; }
        public string SourceSceneId { get; set; } = "";
        public double CloudPercent { get; set; }
        public double ValidPixelPercent { get; set; }
        public bool Reliable { get; set; }
        public DateTimeOffset IngestedUtc { get; set; }
    }

    /// One index's result for one scene (D9) - GridBase64 is null for a backfilled historical scene until its grid is fetched on first view (D10); StatsJson/BoundsJson are always populated at ingest.
    public class ParcelSatelliteIndex
    {
        public int IDParcelSatelliteIndex { get; set; }
        public int SceneID { get; set; }
        public SatelliteIndex Index { get; set; }
        public string? GridBase64 { get; set; }
        public string? BoundsJson { get; set; }
        public string? StatsJson { get; set; }
    }

    public class SatelliteConfigTestRequest
    {
        public string? ClientId { get; set; }
        /// Blank = use whatever ClientSecret is already saved for this tenant, same convention as ArchiveDbTestRequest.
        public string? ClientSecret { get; set; }
    }

    public class SatelliteConfigTestResult
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public string? QuotaSnapshotJson { get; set; }
    }

    /// One point in a Series response - Detaljni dizajn S, B4.
    public class SatelliteSeriesPoint
    {
        public DateOnly SceneDateUtc { get; set; }
        public bool Reliable { get; set; }
        public double? Mean { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? StdDev { get; set; }
    }

    /// One row of the S-B "list scenes for a parcel" response.
    public class SatelliteSceneSummary
    {
        public int IDFarmParcelZoneSatelliteScene { get; set; }
        public int FarmParcelZoneID { get; set; }
        public DateOnly SceneDateUtc { get; set; }
        public double CloudPercent { get; set; }
        public double ValidPixelPercent { get; set; }
        public bool Reliable { get; set; }
        public List<SatelliteIndex> AvailableIndices { get; set; } = [];
    }

    /// The four zoom levels S-C's one map partial renders - "which zones are drawn" is the only thing that changes between them (Detaljni dizajn S, sesija C).
    public enum SatelliteMapScope
    {
        Farm = 1,
        Sowing = 2,
        Parcel = 3,
        Zone = 4,
    }

    /// One zone's row in a SatelliteMapResponse - HasData false means the zone has no scene at/before the requested date yet (still drawn, with an empty raster, per D4's "never disappears").
    public class SatelliteMapZoneEntry
    {
        public int ZoneId { get; set; }
        public string? ZoneName { get; set; }
        public int ParcelId { get; set; }
        public string? GeometryGeoJson { get; set; }
        public bool HasData { get; set; }
        public DateOnly? SceneDateUtc { get; set; }
        public bool Reliable { get; set; }
        /// Non-null only once a rendered index exists - the browser builds the raster URL itself (Agrumy.Web's own /FarmOpenfield/SatelliteImage passthrough), since a URL built here would point at Agrumy.Api's own address, unreachable directly from the browser (different auth: Bearer vs. the web app's cookie).
        public int? SceneId { get; set; }
        public string? StatsJson { get; set; }
    }

    public class SatelliteMapParcelEntry
    {
        public int ParcelId { get; set; }
        public string? ParcelName { get; set; }
        public string? GeometryGeoJson { get; set; }
    }

    public class SatelliteMapResponse
    {
        public List<SatelliteMapZoneEntry> Zones { get; set; } = [];
        public List<SatelliteMapParcelEntry> Parcels { get; set; } = [];
    }
}
