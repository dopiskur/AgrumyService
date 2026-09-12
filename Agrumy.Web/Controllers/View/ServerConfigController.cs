using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StreamPart = Refit.StreamPart; // not `using Refit;` - its AuthorizeAttribute clashes with ASP.NET's

namespace Agrumy.Web.Controllers.View
{
    /// Server Settings page: one read of the whole ServerConfig row, but every tab saves through its own section endpoint so a tab can only change the fields it renders.
    [Authorize]
    public class ServerConfigController(IApi api, IConfiguration configuration) : Controller
    {
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult> Index() => await IndexViewAsync(await api.ServerConfigGet(), TempData["ActiveTab"] as string);

        private async Task<ViewResult> IndexViewAsync(ServerConfig config, string? activeTab)
        {
            await PopulateHealthAsync();
            await PopulateWeatherStateAsync();
            ViewBag.WebhookSsrfAllowlist = await api.WebhookSsrfAllowlistGet();
            // Same config key Program.cs's own Refit HttpClient is built from - the actual base URL a Power BI OData connector would be pointed at.
            ViewBag.ApiServiceUrl = configuration["WebView:ApiService"];
            ViewBag.ActiveTab = activeTab;
            return View("Index", config);
        }

        /// Weather/frost state is per-organization - organization 0 stands in for "the default install location" on this server-wide page, same convention TenantAdminsGetAsync already uses for tenantId 0 = GlobalAdmin.
        private async Task PopulateWeatherStateAsync() => ViewBag.WeatherState = await api.TenantWeatherStateGet(0);

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
        public Task<ActionResult> SaveDeviceDefaults(DeviceDefaultsSettings settings) => SaveSectionAsync(settings, "device-defaults", api.ServerConfigDeviceDefaultsUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveAccounts(AccountSettings settings) => SaveSectionAsync(settings, "accounts", api.ServerConfigAccountsUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveDataRetention(DataRetentionSettings settings) => SaveSectionAsync(settings, "database", api.ServerConfigDataRetentionUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveWeather(WeatherSettings settings) => SaveSectionAsync(settings, "weather", api.ServerConfigWeatherUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveGateway(GatewaySettings settings) => SaveSectionAsync(settings, "gateway", api.ServerConfigGatewayUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveMqtt(MqttSettings settings) => SaveSectionAsync(settings, "mqtt", api.ServerConfigMqttUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveEmail(EmailSettings settings) => SaveSectionAsync(settings, "email", api.ServerConfigEmailUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveWebhook(WebhookSettings settings) => SaveSectionAsync(settings, "webhook", api.ServerConfigWebhookUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveOData(ODataSettings settings) => SaveSectionAsync(settings, "odata", api.ServerConfigODataUpdate);

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<ActionResult> SaveArkod(ArkodSettings settings) => SaveSectionAsync(settings, "arkod", api.ServerConfigArkodUpdate);

        /// PUTs one section; on failure re-renders the page on the same tab with the typed values kept and the API's message under the field it names.
        private async Task<ActionResult> SaveSectionAsync<T>(T settings, string tab, Func<T, Task> put) where T : IServerConfigSection
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await put(settings);
                    TempData["Message"] = "Server settings saved.";
                    TempData["ActiveTab"] = tab;
                    return RedirectToAction(nameof(Index));
                }
                catch (ApiException ex)
                {
                    ModelState.AddModelError(ApiErrorField.Resolve<T>(ex.Body), ex.Body);
                }
            }
            ServerConfig config = await api.ServerConfigGet();
            settings.ApplyTo(config);
            return await IndexViewAsync(config, tab);
        }

        /// Relaxes SsrfGuard's private-IP/https-only checks for this one hostname or CIDR range, webhook-only (FirmwareController.SsrfAllowlistAdd is the separate firmware list). AJAX rather than a form post so it can sit inside the Webhook tab's own form - see webhook-ssrf-allowlist.js.
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

        /// The "Server Health" tab's live-refresh.js poll target - a passive proxy, not an on-demand test button.
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        public async Task<ActionResult> Health() => PartialView("_ServerHealth", await api.ServerConfigGetHealth());

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

        /// Saves the "Data Archiving" subsection on its own - see ServerConfigApiController.SaveArchiveSettings.
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

        /// Offline/manual fallback for the ARKOD GeoPackage sync toggle: an admin who downloaded the file some other way uploads it here instead of waiting on ArkodGeoPackageSyncBackgroundService. AJAX (inline script in Index.cshtml) because the ARKOD tab's own form is not multipart.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(1_200_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 1_200_000_000)]
        public async Task<ActionResult> ArkodGeoPackageUpload(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Choose a .gpkg file first.");
            }
            try
            {
                await using Stream stream = file.OpenReadStream();
                await api.ArkodGeoPackageUpload(new StreamPart(stream, file.FileName, "application/geopackage+sqlite3"));
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode == 0 ? 500 : ex.StatusCode, ex.Body);
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
