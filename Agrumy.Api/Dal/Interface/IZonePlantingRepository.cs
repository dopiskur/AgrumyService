using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// zonePlanting CRUD (Detaljni dizajn R, D8) - Greenhouse's equivalent of Sowing, 1:1 with its DeviceFarmUnitZone. Not wired to any controller yet - the Greenhouse dnevnik UI is a later restructure R session; this facet exists now so that session has a foundation, same reasoning as IFieldLogRepository.
    public interface IZonePlantingRepository
    {
        Task<ZonePlanting?> ZonePlantingGetActiveAsync(int idDeviceFarmUnitZone);

        Task<IList<ZonePlanting>> ZonePlantingsGetAsync(int idDeviceFarmUnitZone);

        Task<ZonePlanting> ZonePlantingStartAsync(ZonePlanting planting);

        Task ZonePlantingCloseAsync(int idZonePlanting);

        /// Agrumy.Api.Migration.TenantImportService only - writes Status/HarvestDate/ClosedUtc verbatim from a source export instead of always creating Active like ZonePlantingStartAsync does, and skips its "one active cycle per zone" guard since the source data already satisfied that invariant (at most one of a given zone's exported cycles has Status==Active).
        Task<ZonePlanting> ZonePlantingRestoreAsync(ZonePlanting planting);
    }
}
