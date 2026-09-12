namespace Agrumy.Dal.Entities
{
    /// See Agrumy.Shared.Models.TenantSatelliteConfig - PK is TenantID itself, one row per organization, no row means the module is off for that organization (D1).
    public class TenantSatelliteConfigRow
    {
        public int TenantID { get; set; }
        public int Provider { get; set; }
        public string? ClientId { get; set; }
        public string? ClientSecretEncrypted { get; set; }
        public int PlanTier { get; set; }
        public int Collection { get; set; }
        public string? CommercialCollectionId { get; set; }
        public string? DefaultIndicesJson { get; set; }
        public int MaxCloudPercent { get; set; }
        public int MinValidPixelPercent { get; set; }
        public bool Enabled { get; set; }
        public DateTimeOffset? LastTokenIssuedUtc { get; set; }
        public string? LastQuotaSnapshotJson { get; set; }
        public DateTimeOffset? QuotaPausedUntilUtc { get; set; }
        public int? RasterRetentionDaysOverride { get; set; }
        public DateTimeOffset? QuotaPausedNotifiedAtUtc { get; set; }
        public int SyncIntervalDays { get; set; } = 1;
        public DateTimeOffset? LastAutoSyncUtc { get; set; }
    }

    /// See Agrumy.Shared.Models.FarmParcelZoneSatelliteScene.
    public class FarmParcelZoneSatelliteSceneRow
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

    /// See Agrumy.Shared.Models.ParcelSatelliteIndex - ImagePath is the PNG cache's on-disk location (satellite-store/{organization}/{zone}/{sceneDate}/{index}.png), regenerated from GridBase64 whenever the retention job has reaped it.
    public class ParcelSatelliteIndexRow
    {
        public int IDParcelSatelliteIndex { get; set; }
        public int SceneID { get; set; }
        public int Index { get; set; }
        public string? GridBase64 { get; set; }
        public string? ImagePath { get; set; }
        public string? BoundsJson { get; set; }
        public string? StatsJson { get; set; }
    }
}
