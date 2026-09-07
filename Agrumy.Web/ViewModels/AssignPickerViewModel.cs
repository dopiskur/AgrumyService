using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    public class AssignPickerViewModel
    {
        public int IDDeviceFarmUnitZone { get; set; }
        public bool ControllerCapable { get; set; }
        public IList<DeviceDto> Devices { get; set; } = new List<DeviceDto>();
    }
}
