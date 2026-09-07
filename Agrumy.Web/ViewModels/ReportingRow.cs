using Agrumy.Shared.Models;

namespace Agrumy.Web.ViewModels
{
    public class ReportingRow
    {
        public required SensorDataReport Report { get; set; }
        public required string DeviceName { get; set; }
    }
}
