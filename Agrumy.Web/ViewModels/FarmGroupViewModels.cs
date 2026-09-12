using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// One row on FarmGroup/Index.cshtml - a group plus how many farms currently belong to it, without fetching each group's full farm list up front.
    public class FarmGroupSummaryViewModel
    {
        public required FarmGroup Group { get; init; }
        public int FarmCount { get; init; }
    }

    /// Drives FarmGroup/Index.cshtml - every Farm Group in scope, plus the "Add Farm Group" form.
    public class FarmGroupIndexViewModel
    {
        public IList<FarmGroupSummaryViewModel> Groups { get; init; } = [];
    }

    /// Drives FarmGroup/Details.cshtml - one group's three vertical sections (Greenhouse/Crop/Fruit); the "ungrouped" lists back each section's "Add" picker (only a farm not already in some group can be added).
    public class FarmGroupDetailsViewModel
    {
        public required FarmGroup Group { get; init; }
        public IList<DeviceFarm> GreenhouseFarms { get; init; } = [];
        public IList<DeviceFarm> CropFarms { get; init; } = [];
        public IList<DeviceFarm> UngroupedGreenhouseFarms { get; init; } = [];
        public IList<DeviceFarm> UngroupedCropFarms { get; init; } = [];
    }
}
