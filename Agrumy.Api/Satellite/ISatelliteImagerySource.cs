using Agrumy.Shared.Models;

namespace Agrumy.Api.Satellite
{
    public record SatelliteCapabilities(IReadOnlyList<SatelliteIndex> Indices, double ResolutionMeters, int RevisitDays, int MaxParcelsPerDay, bool SupportsStatistics);

    public record BoundingBox(double MinLat, double MinLon, double MaxLat, double MaxLon);

    public record SceneCandidate(string SourceSceneId, DateOnly SceneDateUtc, double CloudPercent);

    /// One index rendered for one scene - GridRaw is null when the caller only asked for a PNG render (RenderIndexAsync's PNG path already ran the palette itself); populated when the caller wants the raw grid to persist (the daily job's incremental path).
    public record IndexRender(byte[]? PngBytes, string BoundsJson, string StatsJson, double ValidPixelPercent, byte[]? GridRaw);

    public record QuotaSnapshot(double UsedPercent, string RawJson);

    /// One backfilled scene's stats-only result (Statistical API, D9) - no raster, just the aggregate numbers.
    public record SceneStatEntry(string SourceSceneId, DateOnly SceneDateUtc, double CloudPercent, double ValidPixelPercent, SatelliteIndexStats Stats);

    /// The shape parcelSatelliteIndex.StatsJson is serialized as.
    public sealed class SatelliteIndexStats
    {
        public double? Mean { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? StdDev { get; set; }
        public int SampleCount { get; set; }
    }

    /// Provider abstraction (Detaljni dizajn S, D3) - capabilities carry what a provider CAN do (indices, resolution, revisit, stats support) so the caller never branches on provider identity.
    public interface ISatelliteImagerySource
    {
        /// S-B2 - capabilities depend on which collection the call targets (PlanetScope has no SWIR-derived indices, Pleiades has only visual composites + NDVI), so this takes the collection rather than being a fixed property.
        SatelliteCapabilities GetCapabilities(SatelliteCollection collection);

        Task<IReadOnlyList<SceneCandidate>> FindScenesAsync(int tenantId, SatelliteCollection collection, string? commercialCollectionId, BoundingBox bbox, DateOnly fromUtc, DateOnly toUtc, int maxCloudPercent, CancellationToken ct);

        /// Renders one index for one already-found scene, clipped to the given polygon (not just bbox) - PngBytes/GridRaw depend on renderPng/renderGrid.
        Task<IndexRender> RenderIndexAsync(int tenantId, SatelliteCollection collection, string? commercialCollectionId, SceneCandidate scene, string geoJsonPolygon, SatelliteIndex index, bool renderPng, bool renderGrid, CancellationToken ct);

        /// Statistical API archive backfill (D9) - one call per index across the whole date range, no raster; only meaningful for indices where Capabilities marks SupportsStatistics (visual-only composites don't have a scalar "mean/min/max").
        Task<IReadOnlyList<SceneStatEntry>> BackfillStatisticsAsync(int tenantId, SatelliteCollection collection, string? commercialCollectionId, BoundingBox bbox, string geoJsonPolygon, DateOnly fromUtc, DateOnly toUtc, SatelliteIndex index, int maxCloudPercent, CancellationToken ct);

        Task<QuotaSnapshot> QuotaStatusAsync(int tenantId, CancellationToken ct);
    }

    public interface ISatelliteImagerySourceFactory
    {
        /// Null when the tenant has no config row or Enabled=false - D1, no server-wide fallback.
        Task<ISatelliteImagerySource?> ForAsync(int tenantId, CancellationToken ct);
    }
}
