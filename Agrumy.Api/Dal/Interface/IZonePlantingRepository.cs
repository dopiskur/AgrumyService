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
    }
}
