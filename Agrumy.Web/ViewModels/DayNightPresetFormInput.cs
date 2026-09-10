using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    /// Bound from DeviceFarmUnitController.DayNightPresetAdd's form - a plain HTML form, no client-side tree building needed since the day/night ConditionNode trees are generated server-side (Agrumy.Rules.DayNightTargetPresetBuilder).
    public class DayNightPresetFormInput
    {
        public RelayFunction Function { get; set; }
        public SensorMetric Metric { get; set; }
        public ComparisonOperator Operator { get; set; }
        public double DayValue { get; set; }
        public double NightValue { get; set; }
        public double Hysteresis { get; set; }
        public TimeOnly DayStart { get; set; }
        public TimeOnly DayEnd { get; set; }
        public string NamePrefix { get; set; } = "";
    }
}
