using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Drives DeviceFarmUnit/_FarmsAndUnits.cshtml.
    public class GroupedUnitCubesViewModel
    {
        public IList<DeviceFarmUnitDashboard> Units { get; init; } = [];
        public IList<DeviceFarm> Farms { get; init; } = [];
        /// Open-Field farms' mid-level nodes - shown instead of Units for a farm whose FarmType is OpenField.
        public IList<FarmOpenfieldCrop> Crops { get; init; } = [];
        /// Maps each Open-Field farm to its extension row, since FarmOpenfieldCrop.FarmOpenfieldID points at this, not at the Farm directly.
        public IList<FarmOpenfield> Openfields { get; init; } = [];
    }
}
