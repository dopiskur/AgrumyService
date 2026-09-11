using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// fieldLogEntry/fieldLogAttachment/harvestResult CRUD (Detaljni dizajn R, D6/D7/D14) - one dnevnik shape shared by Open-Field (Sowing/FarmParcelZone) and Greenhouse (ZonePlanting/DeviceFarmUnitZone) scopes.
    public interface IFieldLogRepository
    {
        /// Exactly one of the four scope filters should be non-null on the call site - all four null returns everything, callers don't do that.
        Task<IList<FieldLogEntry>> FieldLogEntriesGetAsync(int? sowingID, int? farmParcelZoneID, int? zonePlantingID, int? deviceFarmUnitZoneID);

        Task<FieldLogEntry?> FieldLogEntryGetByIdAsync(int idFieldLogEntry);

        Task<FieldLogEntry> FieldLogEntryAddAsync(FieldLogEntry entry);

        Task FieldLogEntryDeleteAsync(int idFieldLogEntry);

        Task<IList<FieldLogAttachment>> FieldLogAttachmentsGetAsync(int idFieldLogEntry);

        Task<FieldLogAttachment?> FieldLogAttachmentGetByIdAsync(int idFieldLogAttachment);

        Task<FieldLogAttachment> FieldLogAttachmentAddAsync(FieldLogAttachment attachment);

        Task FieldLogAttachmentDeleteAsync(int idFieldLogAttachment);

        Task<IList<HarvestResult>> HarvestResultsGetAsync(int? sowingID, int? zonePlantingID);

        Task<HarvestResult> HarvestResultAddAsync(HarvestResult result);

        /// The latest (DateUtc.Date + PhiDays) across every PlantProtection entry on the sowing (D13, "iz više prskanja - max") - null if there's never been one, in which case Harvest is never karenca-gated.
        Task<DateOnly?> EarliestHarvestDateAsync(int idSowing);

        /// Sum(Fertilization/BaseFertilization NPercent% x DoseKgPerHa) / sum(AreaHa) across every fertilization entry on the sowing - "bilanca N po ha po sjetvi". Null if the sowing has no fertilization entries yet.
        Task<double?> NitrogenBalanceKgPerHaAsync(int idSowing);
    }
}
