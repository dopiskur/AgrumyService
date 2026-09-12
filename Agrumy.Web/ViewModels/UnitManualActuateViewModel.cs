namespace Agrumy.Web.ViewModels
{
    /// Drives UnitManualActuate.cshtml - the Unit-level manual actuate landing page (mirrors UnitRules' relationship to Zones.cshtml), extracted from Zones.cshtml's own inline card so the button leading here stays visible without pushing that card's bulk into the main page.
    public class UnitManualActuateViewModel
    {
        public required Agrumy.Shared.Models.DeviceFarmUnit Unit { get; init; }
        public required ManualActuateFunctionViewModel Heating { get; init; }
        public required ManualActuateFunctionViewModel Ventilation { get; init; }
        public required ManualActuateFunctionViewModel Irrigation { get; init; }
        public required ManualActuateFunctionViewModel Screen { get; init; }
    }
}
