namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/Index.cshtml, the guided flow for building a zone's (or, roadmap #519, a parcel's) custom dashboard (roadmap #238).
    public class DashboardWizardViewModel
    {
        public IList<ZoneOption> Zones { get; init; } = [];
        public IList<ParcelOption> Parcels { get; init; } = [];
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
