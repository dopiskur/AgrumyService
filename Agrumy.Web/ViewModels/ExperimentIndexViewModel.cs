using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives Experiment/Index.cshtml's Create form - Farms/Units/Zones back the per-scope dropdown (Farm/Unit/Zone name, not a raw id the admin would otherwise have to type/know).
    public class ExperimentIndexViewModel
    {
        public required IList<Experiment> Experiments { get; init; }
        public required IList<DeviceFarm> Farms { get; init; }
        public required IList<DeviceFarmUnit> Units { get; init; }
        public required IList<ZoneOption> Zones { get; init; }
    }
}
