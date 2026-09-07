using System.Globalization;

namespace Agrumy.Web.Utils
{
    /// Roadmap #238 - builds an SVG &lt;polyline&gt; points attribute from a 24-bucket trend array, no charting library needed for a widget this small. Null buckets (no reading that hour) are simply skipped, leaving a gap rather than interpolating a fake value.
    public static class SparklineHelper
    {
        public static string BuildPoints(double?[] values, double width, double height)
        {
            var present = values.Select((v, i) => (Value: v, Index: i)).Where(x => x.Value != null).ToList();
            if (present.Count == 0)
            {
                return "";
            }
            double min = present.Min(x => x.Value!.Value);
            double max = present.Max(x => x.Value!.Value);
            double range = max - min;
            double lastIndex = Math.Max(values.Length - 1, 1);

            var points = present.Select(x =>
            {
                double px = x.Index / lastIndex * width;
                // Flat line at mid-height when every reading this window is identical - avoids a divide-by-zero flattening to the top/bottom edge instead.
                double py = range <= 0 ? height / 2 : height - (x.Value!.Value - min) / range * height;
                return $"{px.ToString(CultureInfo.InvariantCulture)},{py.ToString(CultureInfo.InvariantCulture)}";
            });
            return string.Join(" ", points);
        }
    }
}
