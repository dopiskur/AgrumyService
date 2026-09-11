using Agrumy.Shared.Models;

namespace Agrumy.Api.Dal.Interface
{
    /// Sowing CRUD and its Planned/Active/Closed lifecycle (Detaljni dizajn R, D9/D11) - the Open-Field branch's mid-level rule scope (D5). Occupancy of FarmParcelZones is this facet's job (sowingFarmParcelZone), not IFarmParcelRepository's.
    public interface ISowingRepository
    {
        Task<IList<Sowing>> SowingsGetAsync(int? tenantID);

        Task<Sowing?> SowingGetByIdAsync(int idSowing);

        /// Bare create, Planned status, no zones occupied yet - StartAsync is the step that actually assigns zones and flips CurrentSowingID/Device.SowingID.
        Task<Sowing> SowingAddAsync(Sowing sowing);

        Task SowingUpdateAsync(Sowing sowing);

        /// D3/D11 - occupies every listed FarmParcelZone (all must currently be free, i.e. CurrentSowingID null), writes sowingFarmParcelZone rows, sets FarmParcelZone.CurrentSowingID and every assigned device's Device.SowingID, bumps their ConfigVersion, flips Status to Active. Throws if any zone is already occupied.
        Task SowingStartAsync(int idSowing, IReadOnlyList<int> farmParcelZoneIds);

        /// D9 - releases every zone the sowing occupies (ReleasedUtc, CurrentSowingID/Device.SowingID cleared, ConfigVersion bumped), flips Status to Closed. The caller is responsible for writing the closing fieldLogEntry/harvestResult first (see IFieldLogRepository).
        Task SowingCloseAsync(int idSowing, int? closedByUserID);

        Task SowingDeleteAsync(int idSowing);

        /// The FarmParcelZones this sowing currently occupies (ReleasedUtc null) - Sowing Details' own zone list.
        Task<IList<FarmParcelZone>> SowingOccupiedZonesGetAsync(int idSowing);

        Task<(SensorAverages Averages, SensorTrend Trend)> SowingAggregateAsync(int idSowing);

        Task<IList<SowingDashboard>> SowingDashboardGetAsync(int? tenantID);
    }
}
