using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    public class DeviceCommandButtonsViewModel
    {
        public required CommandTargetType TargetType { get; init; }
        public required int TargetId { get; init; }
    }
}
