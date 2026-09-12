using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// FarmParcel (container) and FarmParcelZone (unit of work) CRUD, split/merge, device assignment (Detaljni dizajn R, D2/D3). Devices/rules/dnevnik attach to the zone, never the parcel directly.
    public interface IFarmParcelRepository
    {
        Task<IList<FarmParcel>> FarmParcelsGetAsync(int idFarm);

        Task<FarmParcel?> FarmParcelGetByIdAsync(int idFarmParcel);

        /// Creates the parcel AND its first zone (IsWholeParcel=true) in one call - D3, every parcel has at least one zone from the moment it exists. quotaCheckAsync (TenantQuotaEnforcer.CheckCanAddFarmParcelZoneAsync) runs inside the same transaction as the insert.
        Task<(FarmParcel Parcel, FarmParcelZone Zone)> FarmParcelAddAsync(FarmParcel parcel, Func<Task<string?>>? quotaCheckAsync = null);

        Task FarmParcelUpdateAsync(FarmParcel parcel);

        /// S-A - sets the parcel's outer boundary; caller passes an already-validated ParcelGeometryValidator result.
        Task FarmParcelGeometrySetAsync(int idFarmParcel, string geometryGeoJson, double areaHectares, double bboxMinLat, double bboxMinLon, double bboxMaxLat, double bboxMaxLon, string? arkodParcelId);

        Task FarmParcelDeleteAsync(int idFarmParcel);

        Task<IList<FarmParcelZone>> FarmParcelZonesGetAsync(int idFarmParcel);

        Task<FarmParcelZone?> FarmParcelZoneGetByIdAsync(int idFarmParcelZone);

        Task FarmParcelZoneUpdateAsync(FarmParcelZone zone);

        /// S-A - sets one zone's subdivision polygon within its parcel's outer boundary.
        Task FarmParcelZoneGeometrySetAsync(int idFarmParcelZone, string geometryGeoJson, double areaHectares, double bboxMinLat, double bboxMinLon, double bboxMaxLat, double bboxMaxLon);

        /// Pre-season field prep confirmed done (or reverted) - independent of SowingStartAsync's own reset-to-false, an admin can flip this either direction any time.
        Task FarmParcelZoneReadyForSeasonSetAsync(int idFarmParcelZone, bool ready);

        /// Bumps ConfigVersion for every device in the zone - mirrors DeviceFarmUnitZoneConfigVersionBumpAsync.
        Task FarmParcelZoneConfigVersionBumpAsync(int idFarmParcelZone);

        /// D3/D4 - replaces one zone with N named zones; blocked (throws) while the source zone has an active sowing (CurrentSowingID != null). quotaCheckAsync (TenantQuotaEnforcer.CheckCanAddFarmParcelZoneAsync, additionalZones = N-1) runs inside the same transaction as the insert - a split is a net add of N-1 zones, not N.
        Task<IList<FarmParcelZone>> FarmParcelZoneSplitAsync(int idFarmParcelZone, IReadOnlyList<string> newZoneNames, Func<Task<string?>>? quotaCheckAsync = null);

        /// D3/D4 - merges N zones of the same parcel back into one; blocked while any source zone has an active sowing.
        Task<FarmParcelZone> FarmParcelZoneMergeAsync(IReadOnlyList<int> farmParcelZoneIds, string mergedName);

        Task FarmParcelZoneDeleteAsync(int idFarmParcelZone);

        Task FarmParcelZoneWidgetsSetAsync(int idFarmParcelZone, List<DashboardWidget> widgets);

        /// Parcel's equivalent of EfDeviceFarmUnitRepository.DeviceFarmUnitZoneGridColumnsSetAsync.
        Task FarmParcelZoneGridColumnsSetAsync(int idFarmParcelZone, int columns);

        Task DeviceAssignToFarmParcelZoneAsync(int idDevice, int idFarmParcelZone);

        Task DeviceUnassignFromFarmParcelZoneAsync(int idDevice);

        Task<bool> FarmParcelZoneHasControllerAsync(int idFarmParcelZone);

        Task<Device?> FarmParcelZoneGetControllerAsync(int idFarmParcelZone);

        Task<IList<Device>> FarmParcelZoneGetSensorsAsync(int idFarmParcelZone);

        Task<(SensorAverages Averages, SensorTrend Trend)> FarmParcelZoneAggregateAsync(int idFarmParcelZone);

        /// Daily-bucketed Moisture average across every device assigned to the zone, for the Zone-tab dual-axis satellite/sensor trend chart.
        Task<IList<FarmParcelZoneMoistureSeriesPoint>> FarmParcelZoneMoistureSeriesGetAsync(int idFarmParcelZone, DateOnly from, DateOnly to);

        Task<IList<FarmParcelZoneDashboard>> FarmParcelZoneDashboardListGetAsync(int idFarmParcel);

        /// S-B - every zone across every organization that has a saved boundary (GeometryGeoJson != null) - the daily satellite job's own per-organization, per-zone loop target.
        Task<IList<FarmParcelZone>> FarmParcelZonesWithGeometryGetAsync(int tenantId);
    }
}
