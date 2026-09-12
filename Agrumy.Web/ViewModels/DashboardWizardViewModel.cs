using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/Index.cshtml, the guided flow for building a zone's (or a parcel's) custom dashboard.
    public class DashboardWizardViewModel
    {
        public IList<ZoneOption> Zones { get; init; } = [];
        public IList<ParcelOption> Parcels { get; init; } = [];
        /// Organization-wide, independent of which zone/parcel is Selected - feeds the wizard's own target step and its scope picker.
        public IList<DeviceFarm> Farms { get; init; } = [];
        public IList<DeviceFarmUnit> Units { get; init; } = [];
        public bool CanManage { get; init; }
        public int? SelectedZoneId { get; init; }
        public int? SelectedParcelId { get; init; }
        public DashboardWidgetsViewModel? Selected { get; init; }
    }

    /// One entry in the wizard's zone picker - GroupLabel is "Farm / Unit" (or just "Unit" when unassigned) for optgroup display.
    public class ZoneOption
    {
        public int IDDeviceFarmUnitZone { get; init; }
        public string ZoneName { get; init; } = "";
        public string GroupLabel { get; init; } = "";
    }
}
