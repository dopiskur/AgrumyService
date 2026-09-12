namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmManualActuate.cshtml - the Farm-level manual actuate landing page, same pattern as UnitManualActuateViewModel one level up.
    public class DeviceFarmManualActuateViewModel
    {
        public required Agrumy.Shared.Models.DeviceFarm Farm { get; init; }
        public required ManualActuateFunctionViewModel Heating { get; init; }
        public required ManualActuateFunctionViewModel Ventilation { get; init; }
        public required ManualActuateFunctionViewModel Irrigation { get; init; }
        public required ManualActuateFunctionViewModel Screen { get; init; }
    }
}
