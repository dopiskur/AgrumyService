using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    [Authorize]
    public class ServerConfigController(IApi api, IConfiguration configuration) : Controller
    {
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult> Index()
        {
            await PopulateHealthAsync();
            ViewBag.WebhookSsrfAllowlist = await api.WebhookSsrfAllowlistGet();
            // Same config key Program.cs's own Refit HttpClient is built from - the actual base URL a Power BI OData connector would be pointed at.
            ViewBag.ApiServiceUrl = configuration["WebView:ApiService"];
            return View(await api.ServerConfigGet());
        }

        /// Relaxes SsrfGuard's private-IP/https-only checks for this one hostname or CIDR range, webhook-only (FirmwareController.SsrfAllowlistAdd is the separate firmware list). AJAX, not a redirect form post, since the Webhook tab lives inside this page's one big Server Settings <form> and can't nest a form of its own - see webhook-ssrf-allowlist.js.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> WebhookSsrfAllowlistAdd([FromBody] SsrfAllowlistEntry entry)
        {
            try
            {
                await api.WebhookSsrfAllowlistAdd(entry);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> WebhookSsrfAllowlistDelete(int id)
        {
            await api.WebhookSsrfAllowlistDelete(id);
            return Ok();
        }

        /// Roadmap #419 - the "Server Health" tab's live-refresh.js poll target (Agrumy.Web's own passive proxy, not an on-demand test button).
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult> Health() => PartialView("_ServerHealth", await api.ServerConfigGetHealth());

        private async Task PopulateHealthAsync()
        {
            // Best-effort - a health-check hiccup must never block the Server Settings page itself from loading/saving.
            try
            {
                ViewBag.ServerHealth = await api.ServerConfigGetHealth();
            }
            catch (ApiException)
            {
                ViewBag.ServerHealth = Array.Empty<ServerHealthEntry>();
            }
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Index(ServerConfig serverConfig)
        {
            await PopulateHealthAsync();
            ViewBag.WebhookSsrfAllowlist = await api.WebhookSsrfAllowlistGet();
            if (!ModelState.IsValid)
            {
                return View(serverConfig);
            }

            try
            {
                await api.ServerConfigUpdate(serverConfig);
            }
            catch (ApiException ex)
            {
                // Route the API's error text to the field it's actually about, else it lands under firmware source by default.
                string field = ex.Body.Contains("firmware", StringComparison.OrdinalIgnoreCase) || ex.Body.Contains("GitHub", StringComparison.OrdinalIgnoreCase)
                    ? nameof(ServerConfig.FirmwareSource)
                    : ex.Body.Contains("cooldown", StringComparison.OrdinalIgnoreCase)
                        ? nameof(ServerConfig.WaterPumpCooldownSeconds)
                        : ex.Body.Contains("WaterPump", StringComparison.OrdinalIgnoreCase)
                            ? nameof(ServerConfig.WaterPumpMaxRunSeconds)
                            : ex.Body.Contains("retention", StringComparison.OrdinalIgnoreCase)
                                ? nameof(ServerConfig.SensorDataRetentionDays)
                                : ex.Body.Contains("latitude", StringComparison.OrdinalIgnoreCase)
                                    ? nameof(ServerConfig.WeatherLocationLat)
                                    : ex.Body.Contains("longitude", StringComparison.OrdinalIgnoreCase)
                                        ? nameof(ServerConfig.WeatherLocationLon)
                                        : ex.Body.Contains("poll interval", StringComparison.OrdinalIgnoreCase)
                                            ? nameof(ServerConfig.WeatherPollIntervalMinutes)
                                            : ex.Body.Contains("rain-skip", StringComparison.OrdinalIgnoreCase)
                                                ? nameof(ServerConfig.WeatherRainSkipThreshold)
                                                : ex.Body.Contains("Frost lookahead", StringComparison.OrdinalIgnoreCase)
                                                    ? nameof(ServerConfig.FrostLookaheadHours)
                                                    : ex.Body.Contains("Frost max cloudiness", StringComparison.OrdinalIgnoreCase)
                                                        ? nameof(ServerConfig.FrostCloudinessMaxPercent)
                                                        : ex.Body.Contains("Frost max wind", StringComparison.OrdinalIgnoreCase)
                                                            ? nameof(ServerConfig.FrostWindMaxMetersPerSecond)
                                                            : ex.Body.Contains("SMTP port", StringComparison.OrdinalIgnoreCase)
                                                                ? nameof(ServerConfig.EmailPort)
                                                                : ex.Body.Contains("email notifications", StringComparison.OrdinalIgnoreCase)
                                                                    ? nameof(ServerConfig.EmailHost)
                                                                    : ex.Body.Contains("PIN validity", StringComparison.OrdinalIgnoreCase)
                                                                        ? nameof(ServerConfig.DevicePinValidMinutes)
                                                                        : ex.Body.Contains("archive", StringComparison.OrdinalIgnoreCase)
                                                                            ? nameof(ServerConfig.ArchiveEnabled)
                                                                            : nameof(ServerConfig.FirmwareSource);
                ModelState.AddModelError(field, ex.Body);
                return View(serverConfig);
            }

            TempData["Message"] = "Server settings saved.";
            return RedirectToAction(nameof(Index));
        }

        /// Sends through the SAVED settings (Save first, then test) - not whatever is currently typed into the unsaved form.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TestEmail(string toEmail)
        {
            try
            {
                await api.ServerConfigTestEmail(toEmail);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        /// Sends through the SAVED settings (Save first, then test) - not whatever is currently typed into the unsaved form.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TestWebhook()
        {
            try
            {
                await api.ServerConfigTestWebhook();
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        /// Tests the CURRENTLY TYPED (unsaved) archive DB fields, opposite of TestEmail above - see ServerConfigApiController.TestArchiveDatabase.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TestArchiveDatabase([FromBody] ArchiveDbTestRequest request)
        {
            try
            {
                await api.ServerConfigTestArchiveDatabase(request);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        /// Saves the whole "Data Archiving" subsection independently of this page's main Save button - see ServerConfigApiController.SaveArchiveSettings.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveArchiveSettings([FromBody] ArchiveSettingsSaveRequest request)
        {
            try
            {
                await api.ServerConfigSaveArchiveSettings(request);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        [HttpGet]
        public async Task<ActionResult<DataMaintenanceProviderInfo>> DataMaintenanceProvider()
        {
            try
            {
                return Ok(await api.DataMaintenanceProviderGet());
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DataMaintenanceOptimize([FromBody] DataMaintenanceRequest request)
        {
            try
            {
                await api.DataMaintenanceOptimize(request);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DataMaintenancePurge([FromBody] DataPurgeRequest request)
        {
            try
            {
                await api.DataMaintenancePurge(request);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DataMaintenancePurgeOrphaned([FromBody] RecycleBinPurgeRequest request)
        {
            try
            {
                await api.DataMaintenancePurgeOrphaned(request);
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }
    }
}
