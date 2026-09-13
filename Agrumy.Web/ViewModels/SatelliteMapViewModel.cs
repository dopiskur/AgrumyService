using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives _SatelliteMap.cshtml, the one map partial every S-C tab (Farm/Sowing/Parcel/Zone) reuses - only Scope/Id change what's drawn, the map/controls/JS are identical.
    public class SatelliteMapViewModel
    {
        public required SatelliteMapScope Scope { get; init; }
        public required int Id { get; init; }
        public IList<SatelliteIndex> AvailableIndices { get; init; } = SatelliteIndexDisplayOrder.Values.ToList();
        public bool CanManage { get; init; }
    }
}
