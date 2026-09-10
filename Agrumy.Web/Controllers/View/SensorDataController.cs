using Agrumy.Web.Security;
using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Agrumy.Web.Controllers.View
{
    [Authorize]
    public class SensorDataController(IApi api) : Controller
    {
        private static SelectList BucketSelectList(SensorDataBucket selected) =>
            new(Enum.GetValues<SensorDataBucket>().Select(b => new { ID = (int)b, Name = b.ToString() }), "ID", "Name", (int)selected);

        public async Task<ActionResult> Index(int? idDevice, DateTimeOffset? from = null, DateTimeOffset? to = null, SensorDataBucket bucket = SensorDataBucket.Hour)
        {
            DateTimeOffset effectiveTo = to ?? DateTimeOffset.UtcNow;
            DateTimeOffset effectiveFrom = from ?? effectiveTo.AddDays(-1);

            var deviceView = new DeviceView
            {
                Device = await api.DeviceGet(idDevice),
                SensorDataFrom = effectiveFrom,
                SensorDataTo = effectiveTo,
                SensorDataBucket = bucket,
            };
            ViewBag.BucketList = BucketSelectList(bucket);

            deviceView.SensorDataJson = await api.SensorDataGet(idDevice, effectiveFrom, effectiveTo, bucket);

            // Chart x-axis shows the user's local time; storage stays UTC.
            string? timeZone = User.GetTimeZone();
            deviceView.SensorDataJson = SensorDataTimeLocalizer.LocalizeDates(deviceView.SensorDataJson, timeZone);
            ViewBag.DisplayTimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone;

            return View(deviceView);
        }

        /// Same live bucketed query as Index, just its own route/form defaulting to a wider day-bucketed range - a formal "report" pull rather than the quick live view Index is for. No more saved snapshots (sensorDataReport is gone) - "generating a report" now just means picking a from/to/bucket and viewing it, same as Index.
        public async Task<ActionResult> Report(int? idDevice, DateTimeOffset? from = null, DateTimeOffset? to = null, SensorDataBucket bucket = SensorDataBucket.Day)
        {
            DateTimeOffset effectiveTo = to ?? DateTimeOffset.UtcNow;
            DateTimeOffset effectiveFrom = from ?? effectiveTo.AddDays(-30);

            var deviceView = new DeviceView
            {
                Device = await api.DeviceGet(idDevice),
                SensorDataFrom = effectiveFrom,
                SensorDataTo = effectiveTo,
                SensorDataBucket = bucket,
            };
            ViewBag.BucketList = BucketSelectList(bucket);

            deviceView.SensorDataJson = await api.SensorDataGet(idDevice, effectiveFrom, effectiveTo, bucket);

            string? timeZone = User.GetTimeZone();
            deviceView.SensorDataJson = SensorDataTimeLocalizer.LocalizeDates(deviceView.SensorDataJson, timeZone);
            ViewBag.DisplayTimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone;

            return View(deviceView);
        }
    }
}
