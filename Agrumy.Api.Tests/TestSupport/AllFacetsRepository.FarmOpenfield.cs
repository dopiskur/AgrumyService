using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Tests.TestSupport
{
    /// IFarmOpenfieldRepository/ISowingRepository/IFarmParcelRepository/ICropCatalogRepository/IFieldLogRepository/IZonePlantingRepository members - forwarded to the standalone Ef*Repository instances so IAllFacetsRepository's broad consumers keep working unchanged (restructure R split the old single IFarmOpenfieldRepository into these five facets).
    internal sealed partial class AllFacetsRepository
    {
        public Task<(DeviceFarm Farm, FarmOpenfield Openfield)> FarmOpenfieldCreateAsync(string? farmName, int? tenantID, Func<Task<string?>>? quotaCheckAsync = null) =>
            farmOpenfieldRepository.FarmOpenfieldCreateAsync(farmName, tenantID, quotaCheckAsync);

        public Task<FarmOpenfield?> FarmOpenfieldGetByFarmIdAsync(int idFarm) => farmOpenfieldRepository.FarmOpenfieldGetByFarmIdAsync(idFarm);

        public Task<IList<FarmOpenfield>> FarmOpenfieldsGetAsync(int? tenantID) => farmOpenfieldRepository.FarmOpenfieldsGetAsync(tenantID);

        // ---- ISowingRepository ----

        public Task<IList<Sowing>> SowingsGetAsync(int? tenantID) => sowingRepository.SowingsGetAsync(tenantID);

        public Task<Sowing?> SowingGetByIdAsync(int idSowing) => sowingRepository.SowingGetByIdAsync(idSowing);

        public Task<Sowing> SowingAddAsync(Sowing sowing) => sowingRepository.SowingAddAsync(sowing);

        public Task SowingUpdateAsync(Sowing sowing) => sowingRepository.SowingUpdateAsync(sowing);

        public Task SowingStartAsync(int idSowing, IReadOnlyList<int> farmParcelZoneIds) => sowingRepository.SowingStartAsync(idSowing, farmParcelZoneIds);

        public Task SowingCloseAsync(int idSowing, int? closedByUserID) => sowingRepository.SowingCloseAsync(idSowing, closedByUserID);

        public Task SowingDeleteAsync(int idSowing) => sowingRepository.SowingDeleteAsync(idSowing);

        public Task<IList<FarmParcelZone>> SowingOccupiedZonesGetAsync(int idSowing) => sowingRepository.SowingOccupiedZonesGetAsync(idSowing);

        public Task<(SensorAverages Averages, SensorTrend Trend)> SowingAggregateAsync(int idSowing) => sowingRepository.SowingAggregateAsync(idSowing);

        public Task<IList<SowingDashboard>> SowingDashboardGetAsync(int? tenantID) => sowingRepository.SowingDashboardGetAsync(tenantID);

        // ---- IFarmParcelRepository ----

        public Task<IList<FarmParcel>> FarmParcelsGetAsync(int idFarmOpenfield) => farmParcelRepository.FarmParcelsGetAsync(idFarmOpenfield);

        public Task<FarmParcel?> FarmParcelGetByIdAsync(int idFarmParcel) => farmParcelRepository.FarmParcelGetByIdAsync(idFarmParcel);

        public Task<(FarmParcel Parcel, FarmParcelZone Zone)> FarmParcelAddAsync(FarmParcel parcel) => farmParcelRepository.FarmParcelAddAsync(parcel);

        public Task FarmParcelUpdateAsync(FarmParcel parcel) => farmParcelRepository.FarmParcelUpdateAsync(parcel);

        public Task FarmParcelGeometrySetAsync(int idFarmParcel, string geometryGeoJson, double areaHectares, double bboxMinLat, double bboxMinLon, double bboxMaxLat, double bboxMaxLon, string? arkodParcelId) =>
            farmParcelRepository.FarmParcelGeometrySetAsync(idFarmParcel, geometryGeoJson, areaHectares, bboxMinLat, bboxMinLon, bboxMaxLat, bboxMaxLon, arkodParcelId);

        public Task FarmParcelDeleteAsync(int idFarmParcel) => farmParcelRepository.FarmParcelDeleteAsync(idFarmParcel);

        public Task<IList<FarmParcelZone>> FarmParcelZonesGetAsync(int idFarmParcel) => farmParcelRepository.FarmParcelZonesGetAsync(idFarmParcel);

        public Task<FarmParcelZone?> FarmParcelZoneGetByIdAsync(int idFarmParcelZone) => farmParcelRepository.FarmParcelZoneGetByIdAsync(idFarmParcelZone);

        public Task FarmParcelZoneUpdateAsync(FarmParcelZone zone) => farmParcelRepository.FarmParcelZoneUpdateAsync(zone);

        public Task FarmParcelZoneGeometrySetAsync(int idFarmParcelZone, string geometryGeoJson, double areaHectares, double bboxMinLat, double bboxMinLon, double bboxMaxLat, double bboxMaxLon) =>
            farmParcelRepository.FarmParcelZoneGeometrySetAsync(idFarmParcelZone, geometryGeoJson, areaHectares, bboxMinLat, bboxMinLon, bboxMaxLat, bboxMaxLon);

        public Task FarmParcelZoneConfigVersionBumpAsync(int idFarmParcelZone) => farmParcelRepository.FarmParcelZoneConfigVersionBumpAsync(idFarmParcelZone);

        public Task<IList<FarmParcelZone>> FarmParcelZoneSplitAsync(int idFarmParcelZone, IReadOnlyList<string> newZoneNames) => farmParcelRepository.FarmParcelZoneSplitAsync(idFarmParcelZone, newZoneNames);

        public Task<FarmParcelZone> FarmParcelZoneMergeAsync(IReadOnlyList<int> farmParcelZoneIds, string mergedName) => farmParcelRepository.FarmParcelZoneMergeAsync(farmParcelZoneIds, mergedName);

        public Task FarmParcelZoneDeleteAsync(int idFarmParcelZone) => farmParcelRepository.FarmParcelZoneDeleteAsync(idFarmParcelZone);

        public Task FarmParcelZoneWidgetsSetAsync(int idFarmParcelZone, List<DashboardWidget> widgets) => farmParcelRepository.FarmParcelZoneWidgetsSetAsync(idFarmParcelZone, widgets);

        public Task FarmParcelZoneGridColumnsSetAsync(int idFarmParcelZone, int columns) => farmParcelRepository.FarmParcelZoneGridColumnsSetAsync(idFarmParcelZone, columns);

        public Task DeviceAssignToFarmParcelZoneAsync(int idDevice, int idFarmParcelZone) => farmParcelRepository.DeviceAssignToFarmParcelZoneAsync(idDevice, idFarmParcelZone);

        public Task DeviceUnassignFromFarmParcelZoneAsync(int idDevice) => farmParcelRepository.DeviceUnassignFromFarmParcelZoneAsync(idDevice);

        public Task<bool> FarmParcelZoneHasControllerAsync(int idFarmParcelZone) => farmParcelRepository.FarmParcelZoneHasControllerAsync(idFarmParcelZone);

        public Task<Device?> FarmParcelZoneGetControllerAsync(int idFarmParcelZone) => farmParcelRepository.FarmParcelZoneGetControllerAsync(idFarmParcelZone);

        public Task<IList<Device>> FarmParcelZoneGetSensorsAsync(int idFarmParcelZone) => farmParcelRepository.FarmParcelZoneGetSensorsAsync(idFarmParcelZone);

        public Task<(SensorAverages Averages, SensorTrend Trend)> FarmParcelZoneAggregateAsync(int idFarmParcelZone) => farmParcelRepository.FarmParcelZoneAggregateAsync(idFarmParcelZone);

        public Task<IList<FarmParcelZoneMoistureSeriesPoint>> FarmParcelZoneMoistureSeriesGetAsync(int idFarmParcelZone, DateOnly from, DateOnly to) => farmParcelRepository.FarmParcelZoneMoistureSeriesGetAsync(idFarmParcelZone, from, to);

        public Task<IList<FarmParcelZoneDashboard>> FarmParcelZoneDashboardListGetAsync(int idFarmParcel) => farmParcelRepository.FarmParcelZoneDashboardListGetAsync(idFarmParcel);

        public Task<IList<FarmParcelZone>> FarmParcelZonesWithGeometryGetAsync(int tenantId) => farmParcelRepository.FarmParcelZonesWithGeometryGetAsync(tenantId);

        // ---- ICropCatalogRepository ----

        public Task<IList<Crop>> CropsGetAsync(int? tenantID) => cropCatalogRepository.CropsGetAsync(tenantID);

        public Task<Crop?> CropGetByIdAsync(int idCrop) => cropCatalogRepository.CropGetByIdAsync(idCrop);

        public Task<Crop> CropAddAsync(Crop crop) => cropCatalogRepository.CropAddAsync(crop);

        public Task CropUpdateAsync(Crop crop) => cropCatalogRepository.CropUpdateAsync(crop);

        public Task CropDeleteAsync(int idCrop) => cropCatalogRepository.CropDeleteAsync(idCrop);

        public Task<int> CropFindOrCreateByNameAsync(int? tenantID, string name) => cropCatalogRepository.CropFindOrCreateByNameAsync(tenantID, name);

        // ---- IFieldLogRepository ----

        public Task<IList<FieldLogEntry>> FieldLogEntriesGetAsync(int? sowingID, int? farmParcelZoneID, int? zonePlantingID, int? deviceFarmUnitZoneID) =>
            fieldLogRepository.FieldLogEntriesGetAsync(sowingID, farmParcelZoneID, zonePlantingID, deviceFarmUnitZoneID);

        public Task<FieldLogEntry?> FieldLogEntryGetByIdAsync(int idFieldLogEntry) => fieldLogRepository.FieldLogEntryGetByIdAsync(idFieldLogEntry);

        public Task<FieldLogEntry> FieldLogEntryAddAsync(FieldLogEntry entry) => fieldLogRepository.FieldLogEntryAddAsync(entry);

        public Task FieldLogEntryDeleteAsync(int idFieldLogEntry) => fieldLogRepository.FieldLogEntryDeleteAsync(idFieldLogEntry);

        public Task<IList<FieldLogAttachment>> FieldLogAttachmentsGetAsync(int idFieldLogEntry) => fieldLogRepository.FieldLogAttachmentsGetAsync(idFieldLogEntry);

        public Task<FieldLogAttachment?> FieldLogAttachmentGetByIdAsync(int idFieldLogAttachment) => fieldLogRepository.FieldLogAttachmentGetByIdAsync(idFieldLogAttachment);

        public Task<FieldLogAttachment> FieldLogAttachmentAddAsync(FieldLogAttachment attachment) => fieldLogRepository.FieldLogAttachmentAddAsync(attachment);

        public Task FieldLogAttachmentDeleteAsync(int idFieldLogAttachment) => fieldLogRepository.FieldLogAttachmentDeleteAsync(idFieldLogAttachment);

        public Task<IList<HarvestResult>> HarvestResultsGetAsync(int? sowingID, int? zonePlantingID) => fieldLogRepository.HarvestResultsGetAsync(sowingID, zonePlantingID);

        public Task<HarvestResult> HarvestResultAddAsync(HarvestResult result) => fieldLogRepository.HarvestResultAddAsync(result);

        public Task<DateOnly?> EarliestHarvestDateAsync(int idSowing) => fieldLogRepository.EarliestHarvestDateAsync(idSowing);

        public Task<double?> NitrogenBalanceKgPerHaAsync(int idSowing) => fieldLogRepository.NitrogenBalanceKgPerHaAsync(idSowing);

        public Task<DateOnly?> EarliestHarvestDateForZonePlantingAsync(int idZonePlanting) => fieldLogRepository.EarliestHarvestDateForZonePlantingAsync(idZonePlanting);

        // ---- IZonePlantingRepository ----

        public Task<ZonePlanting?> ZonePlantingGetActiveAsync(int idDeviceFarmUnitZone) => zonePlantingRepository.ZonePlantingGetActiveAsync(idDeviceFarmUnitZone);

        public Task<IList<ZonePlanting>> ZonePlantingsGetAsync(int idDeviceFarmUnitZone) => zonePlantingRepository.ZonePlantingsGetAsync(idDeviceFarmUnitZone);

        public Task<ZonePlanting> ZonePlantingStartAsync(ZonePlanting planting) => zonePlantingRepository.ZonePlantingStartAsync(planting);

        public Task ZonePlantingCloseAsync(int idZonePlanting) => zonePlantingRepository.ZonePlantingCloseAsync(idZonePlanting);
    }
}
