using System.Text.Json.Serialization;
using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StreamPart = Refit.StreamPart; // not `using Refit;` - its AuthorizeAttribute clashes with ASP.NET's

namespace Agrumy.Web.Controllers.View
{
    [Authorize(Roles = RoleNames.GlobalAdmin)]
    public class FirmwareController(IApi api, IConfiguration configuration) : Controller
    {
        // Bare host, matching what the captive portal's own servicePoint field expects (Agrumy.Shared.Models.DeviceRegistration) - WebView:ApiService is a full URL (e.g. "https://api.agrumy.com"), Agrumy.Api itself, NOT this Web app's own host.
        private string ApiServicePointHost => new Uri(configuration["WebView:ApiService"]!).Host;

        public async Task<ActionResult> Index()
        {
            ServerConfig config = await api.ServerConfigGet();
            IList<DeviceFirmware> catalog = await api.FirmwareList(null);

            return View(new FirmwareViewModel
            {
                Config = config,
                Catalog = catalog,
                InstallableBoards = InstallableBoards(catalog, config),
            });
        }

        private static List<DeviceFirmware> InstallableBoards(IList<DeviceFirmware> catalog, ServerConfig config) =>
            catalog
                .Where(f => f.Board != null && f.FullImageFileName != null && (f.Source == config.FirmwareSource || f.Source == FirmwareSource.Local))
                .GroupBy(f => f.Board!)
                .Select(g => g.First())
                .ToList();

        // ServerConfigApiController.Update overwrites the whole row, so this re-fetches and overlays only these three fields to avoid clobbering concurrent Server Settings changes.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveSettings(FirmwareSource firmwareSource, string? firmwareGitHubRepository, string? firmwareCustomRepositoryUrl, int? firmwareRefreshIntervalHours)
        {
            ServerConfig config = await api.ServerConfigGet();
            config.FirmwareSource = firmwareSource;
            config.FirmwareGitHubRepository = firmwareGitHubRepository;
            config.FirmwareCustomRepositoryUrl = firmwareCustomRepositoryUrl;
            config.FirmwareRefreshIntervalHours = firmwareRefreshIntervalHours;
            try
            {
                await api.ServerConfigUpdate(config);
                TempData["Message"] = "Firmware source settings saved.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Sync(FirmwareSyncMode mode)
        {
            await RunAndReport(() => api.FirmwareSync(new FirmwareSyncRequest { Mode = mode }));
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Import(string path)
        {
            await RunAndReport(() => api.FirmwareImport(new FirmwareImportRequest { Path = path }));
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Upload(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Choose a .bin or .zip file first.";
                return RedirectToAction(nameof(Index));
            }
            try
            {
                await using Stream stream = file.OpenReadStream();
                if (string.Equals(Path.GetExtension(file.FileName), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    FirmwareSyncResult result = await api.FirmwareUploadZip(new StreamPart(stream, file.FileName, "application/zip"));
                    TempData["Message"] = $"Done: {result.Added} added, {result.Skipped} skipped, {result.Removed} removed.";
                    if (result.Warnings.Count > 0)
                    {
                        TempData["Error"] = string.Join(" | ", result.Warnings);
                    }
                }
                else
                {
                    DeviceFirmware added = await api.FirmwareUpload(new StreamPart(stream, file.FileName, "application/octet-stream"));
                    TempData["Message"] = $"Uploaded {added.FileName} ({added.Board} {added.Version}).";
                }
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index));
        }

        /// "Build from GitHub repository": packages the visible catalog into a ZIP the browser downloads directly - <paramref name="latestOnly"/> keeps just the newest build per board, otherwise every visible build is included.
        public async Task<ActionResult> DownloadZip(bool latestOnly)
        {
            HttpResponseMessage response = await api.FirmwareDownloadZip(latestOnly);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }
            string downloadName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "agrumy-firmware.zip";
            return File(await response.Content.ReadAsStreamAsync(), "application/zip", downloadName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int idDeviceFirmware)
        {
            await api.FirmwareDelete(idDeviceFirmware);
            return RedirectToAction(nameof(Index));
        }

        // ---- Post-flash provisioning wizard - AJAX endpoints backing firmware-provisioning.js ----

        /// Auto-fills the wizard's userLogin/devicePin instead of asking the admin to retype what Device/AddDevice already shows them - same "reuse caller's still-valid PIN" mechanism, minted fresh only when the current one is missing/expired.
        [HttpGet]
        public async Task<ActionResult> MyProvisioningCredentials()
        {
            User self = await api.UserGetSelf();
            bool stillValid = !string.IsNullOrEmpty(self.DevicePin) &&
                self.DevicePinExpires is DateTimeOffset expires && expires > DateTimeOffset.UtcNow;
            string? devicePin = stillValid ? self.DevicePin : (await api.DevicePinGenerate()).DevicePin;
            return Json(new { email = self.Email, devicePin, servicePoint = ApiServicePointHost });
        }

        /// Ssid-only - the caller never needs the real Password (see ResolveWifiSecret below, used only at the moment of sending it to the device over serial).
        [HttpGet]
        public async Task<ActionResult> WifiConfigsForProvisioning() =>
            Json((await api.DiscoveryWifiConfigsGet()).Select(c => new { id = c.IDTenantWifiConfig, ssid = c.Ssid }));

        /// The one place this wizard needs the real WiFi password - resolved just-in-time, never persisted client-side beyond the moment it's written to the device's serial port.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResolveWifiSecret(int idTenantWifiConfig)
        {
            try
            {
                TenantWifiConfig config = await api.DiscoveryWifiConfigReveal(idTenantWifiConfig);
                return Json(new { ssid = config.Ssid, password = config.Password });
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        /// Farm > Unit > Zone, nested - the wizard needs the whole shape up front to decide whether to show a Farm picker at all ("one farm = no farm-object" principle extends here: skip straight to Unit > Zone when there's only one).
        [HttpGet]
        public async Task<ActionResult> FarmTree()
        {
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            IList<DeviceFarmUnit> units = await api.DeviceFarmUnitsGet();
            var zonesByUnit = new Dictionary<int, IList<DeviceFarmUnitZone>>();
            foreach (DeviceFarmUnit unit in units)
            {
                zonesByUnit[unit.IDDeviceFarmUnit!.Value] = await api.DeviceFarmUnitZonesGet(unit.IDDeviceFarmUnit);
            }

            object UnitNode(DeviceFarmUnit u) => new
            {
                id = u.IDDeviceFarmUnit,
                name = u.DeviceFarmUnitName,
                zones = zonesByUnit[u.IDDeviceFarmUnit!.Value].Select(z => new { id = z.IDDeviceFarmUnitZone, name = z.DeviceFarmUnitZoneName }),
            };

            return Json(new
            {
                farms = farms.Select(f => new
                {
                    id = f.IDDeviceFarm,
                    name = f.DeviceFarmName,
                    units = units.Where(u => u.DeviceFarmID == f.IDDeviceFarm).Select(UnitNode),
                }),
                unassignedUnits = units.Where(u => u.DeviceFarmID == null).Select(UnitNode),
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AssignProvisionedDevice(int idDevice, int idDeviceFarmUnitZone)
        {
            try
            {
                await api.DeviceAssign(new DeviceZoneAssignment { IDDevice = idDevice, IDDeviceFarmUnitZone = idDeviceFarmUnitZone });
                return Ok();
            }
            catch (ApiException ex)
            {
                return StatusCode(ex.StatusCode, ex.Body);
            }
        }

        public async Task<ActionResult> OfflineFile(string fileName)
        {
            HttpResponseMessage response = await api.FirmwareFetch(fileName);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode);
            }
            string downloadName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? fileName;
            return File(await response.Content.ReadAsStreamAsync(), "application/octet-stream", downloadName);
        }

        public async Task<ActionResult> InstallManifest(string board)
        {
            string? chipFamily = EspChipFamily.ForBoard(board);
            if (chipFamily == null)
            {
                return NotFound();
            }
            DeviceFirmware? latest = (await api.FirmwareList(board)).FirstOrDefault(f => f.FullImageFileName != null);
            if (latest == null)
            {
                return NotFound();
            }
            return Json(new EspWebToolsManifest(
                $"Agrumy {board}",
                latest.Version ?? "unknown",
                NewInstallPromptErase: true,
                [new EspWebToolsBuild(chipFamily, [new EspWebToolsPart(Url.Action(nameof(OfflineFile), new { fileName = latest.FullImageFileName })!, Offset: 0)])]
            ));
        }

        private sealed record EspWebToolsManifest(string Name, string Version,
            [property: JsonPropertyName("new_install_prompt_erase")] bool NewInstallPromptErase, List<EspWebToolsBuild> Builds);
        private sealed record EspWebToolsBuild(string ChipFamily, List<EspWebToolsPart> Parts);
        private sealed record EspWebToolsPart(string Path, int Offset);

        private async Task RunAndReport(Func<Task<FirmwareSyncResult>> action)
        {
            try
            {
                FirmwareSyncResult result = await action();
                TempData["Message"] = $"Done: {result.Added} added, {result.Skipped} skipped, {result.Removed} removed.";
                if (result.Warnings.Count > 0)
                {
                    TempData["Error"] = string.Join(" | ", result.Warnings);
                }
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
        }
    }
}
