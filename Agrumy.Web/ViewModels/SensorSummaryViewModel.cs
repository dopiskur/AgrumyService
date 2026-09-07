using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    public class SensorSummaryViewModel
    {
        public required SensorAverages Averages { get; init; }
        public SensorTrend? Trend { get; init; }
    }
}
