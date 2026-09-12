using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Migration;
using Agrumy.Api.Satellite;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Shared.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Organization Management CRUD - write is Global admin only since an organization has no meaningful self-management of its own existence, unlike Device/User management.
    [Route("/api/Tenant")]
    public class TenantApiController(ITenantRepository tenantRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IDeviceRepository deviceRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, ICache cache, TenantExportService exportService, TenantImportService importService, DeviceOutboxService commandQueue, ISatelliteConfigRepository satelliteConfigRepo, ICdseTokenProvider cdseTokenProvider) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        [Authorize(Roles = RoleNames.GlobalAdminOrReader)]
        [HttpGet("All")]
        public async Task<ActionResult<IList<Tenant>>> TenantsGet()
        {
            if (!CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader))
            {
                return ForbidWith("Tenant Management requires the Global admin or Global reader role");
            }
            return Ok(await tenantRepo.TenantsGetAllAsync());
        }

        /// Any authenticated caller may look up their OWN organization (e.g. UserController.Edit/Details resolving a TenantName to display) - only a cross-organization lookup needs the Global admin/reader role.
        [Authorize]
        [HttpGet]
        public async Task<ActionResult<Tenant>> TenantGet(int idTenant)
        {
            if (!CallerIsGlobalAdmin && !CallerHasRole(RoleNames.GlobalReader) && idTenant != CallerTenantId)
            {
                return ForbidWith("Tenant Management requires the Global admin or Global reader role");
            }
            Tenant? tenant = await tenantRepo.TenantGetByIdAsync(idTenant);
            return tenant is null ? NotFound() : Ok(tenant);
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost]
        public async Task<ActionResult<int>> TenantAdd([FromBody] Tenant tenant)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Creating a tenant requires the Global admin role");
            }
            if (string.IsNullOrWhiteSpace(tenant.TenantName))
            {
                return BadRequest("Tenant name is required.");
            }
            int idTenant = await tenantRepo.TenantAddAsync(tenant.TenantName.Trim());
            await deviceFarmUnitRepo.EnsureFirstFarmAsync(idTenant); // Same as the self-service registration path.
            return Ok(idTenant);
        }

        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPut]
        public async Task<ActionResult> TenantUpdate([FromBody] Tenant tenant)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Renaming a tenant requires the Global admin role");
            }
            if (tenant.IDTenant is null)
            {
                return BadRequest("IDTenant is required.");
            }
            if (string.IsNullOrWhiteSpace(tenant.TenantName))
            {
                return BadRequest("Tenant name is required.");
            }
            tenant.TenantName = tenant.TenantName.Trim();

            // A bad id would silently degrade every device in this organization's schedule mode to UTC (TimeZoneHelper.GetUtcOffsetSeconds' fallback) instead of failing at save time; blank/null clears it back to "not configured".
            if (!string.IsNullOrWhiteSpace(tenant.ScheduleTimeZone))
            {
                if (!TimeZoneHelper.TryNormalizeToIana(tenant.ScheduleTimeZone, out string iana))
                {
                    return BadRequest("Unknown time zone: " + tenant.ScheduleTimeZone);
                }
                tenant.ScheduleTimeZone = iana;
            }
            else
            {
                tenant.ScheduleTimeZone = null;
            }

            // Same pairing/range checks as ServerConfigApiController.Update's WeatherLocationLat/Lon - one set without the other silently degrades AstronomicalRuleResolver back to the server-wide fallback instead of failing at save time.
            if (tenant.Latitude.HasValue != tenant.Longitude.HasValue)
            {
                return BadRequest("Latitude and Longitude must be set together, or both left blank.");
            }
            if (tenant.Latitude is < -90 or > 90)
            {
                return BadRequest("Latitude must be between -90 and 90.");
            }
            if (tenant.Longitude is < -180 or > 180)
            {
                return BadRequest("Longitude must be between -180 and 180.");
            }

            await tenantRepo.TenantUpdateAsync(tenant);
            return Ok();
        }

        /// GlobalAdmin only, same bar as TenantAdd - deletes the organization row plus its Wifi configs/quota/usage snapshots; an organization still holding any device is refused outright (device organization reassignment has no UI yet, so a stray orphaned device would be unrecoverable). deleteUsers=false leaves the organization's users behind with TenantID cleared to null ("Unassigned"); true deletes them too - never automatic, always the caller's explicit choice.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpDelete]
        public async Task<ActionResult> TenantDelete(int idTenant, bool deleteUsers = false)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Deleting a tenant requires the Global admin role");
            }
            if (idTenant == 0)
            {
                return BadRequest("The default tenant (0) cannot be deleted.");
            }
            Tenant? tenant = await tenantRepo.TenantGetByIdAsync(idTenant);
            if (tenant is null)
            {
                return NotFound();
            }
            IList<DeviceFleetStatus> devices = await deviceRepo.DeviceFleetGetAsync(idTenant);
            if (devices.Count > 0)
            {
                return StatusCode(409, $"Tenant still has {devices.Count} device(s) - reassign or delete them before deleting the tenant.");
            }

            bool deleted = await tenantRepo.TenantDeleteAsync(idTenant, deleteUsers);
            if (deleted)
            {
                await WriteAuditAsync("Tenant.Deleted", idTenant, "Tenant", idTenant.ToString(), $"deleteUsers={deleteUsers}");
            }
            return deleted ? Ok() : NotFound();
        }

        // ---- Alert config -----------------------------------------------------

        /// Always the caller's own organization - an Organization admin's Battery/Tank/problem-event alert overrides, no idTenant parameter to avoid needing a separate ownership check. Global reader can view (read-only, same as the ServerConfig-backed Alerts page), write stays Admins-only below.
        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        [HttpGet("AlertConfig")]
        public async Task<ActionResult<TenantAlertConfig>> TenantAlertConfigGet()
        {
            if (CallerTenantId is not int tenantId)
            {
                return ForbidWith("Caller has no tenant.");
            }
            return Ok(await tenantRepo.TenantAlertConfigGetAsync(tenantId));
        }

        [Authorize(Roles = RoleNames.Admins)]
        [HttpPut("AlertConfig")]
        public async Task<ActionResult> TenantAlertConfigUpdate([FromBody] TenantAlertConfig config)
        {
            if (CallerTenantId is not int tenantId)
            {
                return ForbidWith("Caller has no tenant.");
            }
            if (config.ProblemEventExpiryHours is int hours && hours is not (1 or 6 or 12 or 24 or 48))
            {
                return BadRequest("ProblemEventExpiryHours must be one of 1, 6, 12, 24, 48, or left blank.");
            }
            await tenantRepo.TenantAlertConfigUpdateAsync(tenantId, config);
            await WriteAuditAsync("Tenant.AlertConfigUpdated", tenantId, "Tenant", tenantId.ToString(), null);
            return Ok();
        }

        /// idTenant defaults to the caller's own organization - same optional-query-param shape as EmergencyStopStatus above, so an Organization admin's plain GET "just works" while a Global admin/reader can still target any organization explicitly. Read-only (WeatherEvaluator/FrostAlertEvaluator are the only writers), so no PUT counterpart.
        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        [HttpGet("WeatherState")]
        public async Task<ActionResult<TenantWeatherState>> WeatherStateGet(int? idTenant = null)
        {
            int targetTenantId = idTenant ?? CallerTenantId ?? -1;
            if (!CallerReadsTenantConfig(targetTenantId))
            {
                return ForbidWith("Not authorized to view this tenant's weather state.");
            }
            return Ok(await tenantRepo.TenantWeatherStateGetAsync(targetTenantId));
        }

        // ---- Emergency stop -----------------------------------

        /// Fail-closed, organization-wide: forces every actuator in idTenant (defaulting to the caller's own) off ahead of any rule, until explicitly cleared. Deliberately one click, no confirmation - unlike a destructive action, hesitation here is the wrong default.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("EmergencyStop")]
        public async Task<ActionResult> EmergencyStopActivate(int? idTenant = null)
        {
            int targetTenantId = idTenant ?? CallerTenantId ?? -1;
            if (!CallerManagesDevices(targetTenantId))
            {
                return ForbidWith("Emergency stop requires managing devices in this tenant.");
            }
            await tenantRepo.TenantEmergencyStopSetAsync(targetTenantId, true);
            await WriteAuditAsync("Tenant.EmergencyStopActivated", targetTenantId, "Tenant", targetTenantId.ToString(), null);
            // Instant push, not just "wait for next poll" - every other urgent command already gets this via MqttCommandPublisher.
            await commandQueue.IssueTenantWideCommandAsync(targetTenantId, CommandActionType.ForceConfigSync);
            return Ok();
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("EmergencyStop/Clear")]
        public async Task<ActionResult> EmergencyStopClear(int? idTenant = null)
        {
            int targetTenantId = idTenant ?? CallerTenantId ?? -1;
            if (!CallerManagesDevices(targetTenantId))
            {
                return ForbidWith("Emergency stop requires managing devices in this tenant.");
            }
            await tenantRepo.TenantEmergencyStopSetAsync(targetTenantId, false);
            await WriteAuditAsync("Tenant.EmergencyStopCleared", targetTenantId, "Tenant", targetTenantId.ToString(), null);
            await commandQueue.IssueTenantWideCommandAsync(targetTenantId, CommandActionType.ForceConfigSync);
            return Ok();
        }

        /// Lets the Web layout show a persistent banner without needing the wider TenantReaders role TenantGet requires.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpGet("EmergencyStop")]
        public async Task<ActionResult<bool>> EmergencyStopStatus(int? idTenant = null)
        {
            int targetTenantId = idTenant ?? CallerTenantId ?? -1;
            if (!CallerManagesDevices(targetTenantId))
            {
                return ForbidWith("Emergency stop requires managing devices in this tenant.");
            }
            Tenant? tenant = await tenantRepo.TenantGetByIdAsync(targetTenantId);
            return Ok(tenant?.EmergencyStopActive ?? false);
        }

        // ---- Export/Import --------------------------------------------------

        /// SENSITIVE: carries every exported user's password hash/salt and device's ApiKey (treat like a credential bundle, never persisted server-side, built in memory and streamed straight back) - a TenantAdmin exports only their OWN organization, Global admin any. ZIP-packaged (single export.json entry, see TenantExportService.BuildExportZipAsync) - same repackaging already applies to the firmware catalog.
        [Authorize(Roles = RoleNames.Admins)]
        [HttpGet("Export")]
        public async Task<ActionResult> Export(int idTenant, bool includeSensorData = false, DateTime? sensorDataSinceUtc = null, CancellationToken cancellationToken = default)
        {
            if (!CallerIsGlobalAdmin && !(CallerHasRole(RoleNames.TenantAdmin) && CallerTenantId == idTenant))
            {
                return ForbidWith("Exporting a tenant requires being its Tenant admin, or Global admin.");
            }
            (Stream content, string fileName) = await exportService.BuildExportZipAsync(idTenant, includeSensorData, sensorDataSinceUtc, cancellationToken);
            await WriteAuditAsync("Tenant.Exported", idTenant, "Tenant", idTenant.ToString(), $"includeSensorData={includeSensorData}");
            return File(content, "application/zip", fileName);
        }

        /// ByName only (see Agrumy.Shared.Models.TenantImportTarget), Global admin only - unlike Export this can create a brand-new organization or add into one the caller doesn't administer, same bar as TenantAdd/TenantUpdate.
        [Authorize(Roles = RoleNames.GlobalAdmin)]
        [HttpPost("Import")]
        public async Task<ActionResult<TenantImportResult>> Import([FromBody] TenantImportRequest value)
        {
            if (!CallerIsGlobalAdmin)
            {
                return ForbidWith("Importing a tenant requires the Global admin role");
            }
            if (value.Export is null)
            {
                return BadRequest("Export is required.");
            }
            if (string.IsNullOrWhiteSpace(value.TargetTenantName))
            {
                return BadRequest("TargetTenantName is required.");
            }
            if (value.Export.FormatVersion != TenantExport.CurrentFormatVersion)
            {
                return BadRequest($"Unsupported export format version '{value.Export.FormatVersion}' - this server understands '{TenantExport.CurrentFormatVersion}'.");
            }
            return Ok(await importService.ImportByNameAsync(value.Export, value.TargetTenantName.Trim()));
        }

        /// AsSentinel claims TenantID=0 with this export's users/devices, replacing the unclaimed bootstrap admin placeholder (see TenantImportService.ImportAsSentinelAsync, ITenantRepository.TenantZeroIsEmptyAsync) - deliberately anonymous since a brand-new self-hosted server has nobody to authenticate as yet, TenantZeroIsEmptyAsync itself blocks this against an already-provisioned server.
        [AllowAnonymous]
        [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
        [HttpPost("ImportAsSentinel")]
        public async Task<ActionResult<TenantImportResult>> ImportAsSentinel([FromBody] TenantExport value)
        {
            if (value.FormatVersion != TenantExport.CurrentFormatVersion)
            {
                return BadRequest($"Unsupported export format version '{value.FormatVersion}' - this server understands '{TenantExport.CurrentFormatVersion}'.");
            }
            var (result, error) = await importService.ImportAsSentinelAsync(value);
            return error != null ? StatusCode(409, error) : Ok(result);
        }

        #region Satellite module config (Detaljni dizajn S, D1/D8/D11/D12)

        private static readonly int[] AllowedSyncIntervalDays = [1, 2, 3, 4, 5, 6, 7, 30];

        /// idTenant defaults to the caller's own organization - same optional-query-param shape as EmergencyStopActivate above, so an Organization admin's plain GET/PUT "just works" for their own organization while a Global admin can still target any organization explicitly.
        [Authorize(Roles = RoleNames.AdminsOrGlobalReader)]
        [HttpGet("Satellite")]
        public async Task<ActionResult<TenantSatelliteConfig>> SatelliteConfigGet(int? idTenant = null)
        {
            int targetTenantId = idTenant ?? CallerTenantId ?? -1;
            if (!CallerReadsTenantConfig(targetTenantId))
            {
                return ForbidWith("Not authorized to view this tenant's satellite config.");
            }
            TenantSatelliteConfig config = await satelliteConfigRepo.SatelliteConfigGetAsync(targetTenantId) ?? new TenantSatelliteConfig { IDTenant = targetTenantId };
            config.ClientSecret = null; // write-only - HasSecret already tells the caller whether one is configured
            return Ok(config);
        }

        [Authorize(Roles = RoleNames.Admins)]
        [HttpPut("Satellite")]
        public async Task<ActionResult<TenantSatelliteConfig>> SatelliteConfigPut([FromBody] TenantSatelliteConfig config, int? idTenant = null)
        {
            int targetTenantId = idTenant ?? CallerTenantId ?? -1;
            if (!CallerManagesTenantConfig(targetTenantId))
            {
                return ForbidWith("Not authorized to change this tenant's satellite config.");
            }
            if (config.MaxCloudPercent is < 0 or > 100)
            {
                return BadRequest("MaxCloudPercent must be 0-100.");
            }
            if (config.MinValidPixelPercent is < 0 or > 100)
            {
                return BadRequest("MinValidPixelPercent must be 0-100.");
            }
            if (!AllowedSyncIntervalDays.Contains(config.SyncIntervalDays))
            {
                return BadRequest($"SyncIntervalDays must be one of: {string.Join(", ", AllowedSyncIntervalDays)}.");
            }
            config.IDTenant = targetTenantId;
            TenantSatelliteConfig saved = await satelliteConfigRepo.SatelliteConfigUpsertAsync(config);
            saved.ClientSecret = null;
            await WriteAuditAsync("Tenant.SatelliteConfigSet", targetTenantId, "Tenant", targetTenantId.ToString(), $"Provider={config.Provider}, Enabled={config.Enabled}");
            return Ok(saved);
        }

        /// D1 - one token request + one lightweight Catalog query, same "test before/after save" shape as ServerConfigApiController.TestArchiveDatabase; blank ClientSecret in the request falls back to whatever's already saved for this organization. On success the tested credentials are saved immediately (this endpoint doubles as "test and save") and LastTokenIssuedUtc refreshes so the settings page's health indicator reflects this test too.
        [Authorize(Roles = RoleNames.Admins)]
        [HttpPost("Satellite/Test")]
        public async Task<ActionResult<SatelliteConfigTestResult>> SatelliteConfigTest([FromBody] SatelliteConfigTestRequest request, int? idTenant = null)
        {
            int targetTenantId = idTenant ?? CallerTenantId ?? -1;
            if (!CallerManagesTenantConfig(targetTenantId))
            {
                return ForbidWith("Not authorized to test this tenant's satellite config.");
            }
            string? clientId = request.ClientId;
            string? clientSecret = request.ClientSecret;
            if (string.IsNullOrEmpty(clientSecret))
            {
                (string? savedClientId, string? savedClientSecret) = await satelliteConfigRepo.SatelliteConfigCredentialsGetAsync(targetTenantId);
                clientId ??= savedClientId;
                clientSecret = savedClientSecret;
            }
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return Ok(new SatelliteConfigTestResult { Ok = false, Error = "No client ID/secret to test - fill in both fields or save a config first." });
            }

            (bool ok, string? error) = await cdseTokenProvider.TryGetAccessTokenForCredentialsAsync(clientId, clientSecret, HttpContext.RequestAborted);
            if (ok)
            {
                await satelliteConfigRepo.SatelliteConfigTokenIssuedAsync(targetTenantId, DateTimeOffset.UtcNow);
                TenantSatelliteConfig config = await satelliteConfigRepo.SatelliteConfigGetAsync(targetTenantId) ?? new TenantSatelliteConfig { IDTenant = targetTenantId };
                config.IDTenant = targetTenantId;
                config.ClientId = clientId;
                config.ClientSecret = clientSecret;
                await satelliteConfigRepo.SatelliteConfigUpsertAsync(config);
            }
            await WriteAuditAsync("Tenant.SatelliteTest", targetTenantId, "Tenant", targetTenantId.ToString(), ok ? "ok, credentials saved" : error);
            return Ok(new SatelliteConfigTestResult { Ok = ok, Error = error });
        }

        #endregion

        /// GET: Global admin/reader can view any organization; an Organization admin can view only their own. Same shape as CallerManagesDevices, but for organization-settings ownership rather than device-management roles.
        private bool CallerReadsTenantConfig(int targetTenantId) =>
            CallerManagesTenantConfig(targetTenantId) || CallerHasRole(RoleNames.GlobalReader);

        /// PUT/Test: Global admin can manage any organization; an Organization admin only their own.
        private bool CallerManagesTenantConfig(int targetTenantId) =>
            CallerIsGlobalAdmin || (CallerHasRole(RoleNames.TenantAdmin) && targetTenantId == CallerTenantId);
    }
}
