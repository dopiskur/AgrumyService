using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives Experiment/Index.cshtml's Create form - Farms/Units/Zones/Crops/Parcels back the per-scope dropdown (a name, not a raw id the admin would otherwise have to type/know).
    public class ExperimentIndexViewModel
    {
        public required IList<Experiment> Experiments { get; init; }
        public required IList<DeviceFarm> Farms { get; init; }
        public required IList<DeviceFarmUnit> Units { get; init; }
        public required IList<ZoneOption> Zones { get; init; }
        public required IList<Sowing> Crops { get; init; }
        public required IList<ParcelOption> Parcels { get; init; }
    }

    /// Same "Farm / Crop name" grouped-label convention as ZoneOption, for the Open-Field branch.
    public class ParcelOption
    {
        public int IDFarmParcelZone { get; init; }
        public string ParcelName { get; init; } = "";
        public string GroupLabel { get; init; } = "";
    }
}
