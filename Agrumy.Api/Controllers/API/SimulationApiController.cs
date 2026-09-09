using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Devices;
using Agrumy.Shared;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Api.Simulation;
using Agrumy.Api.Quota;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Controllers.API
{
    /// Admin-facing create/list/delete for fully virtual devices - the actual per-tick simulation runs in Agrumy.Api.BackgroundWorkers.VirtualDeviceRunnerBackgroundService, not here.
    [Route("/api/Simulation")]
    public class SimulationApiController(ISimulationRepository simulationRepo, IDeviceRepository deviceRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, IServerConfigRepository serverConfigRepo, ICache cache, IHttpClientFactory httpClientFactory, Agrumy.Api.Quota.TenantQuotaEnforcer quotaEnforcer, RuleValidationService ruleValidation, IOptions<AgrumySettings> settingsOptions) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        private readonly AgrumySettings settings = settingsOptions.Value;
        // Same ceiling DeviceFarmUnitApiController's Zone/Unit/Farm/Global rule routes use - a simulation session's own rule list is one more scope, no reason for a different cap.
        private const int HardMaxRulesPerZone = 32;

        // Separate field, not the primary-constructor parameter directly - a parameter used both here and in the base(...) call trips CS9107 (ambiguous double-capture).
        private readonly IUserRepository users = userRepo;
        /// Creates the device via the SAME POST /api/Device/Register a real device calls after WiFi setup (Option C design - the endpoint never learns the caller isn't real hardware), then tags it in the virtual-device registry so the background runner picks it up. Deliberately bare - no Name/Unit/Zone here, an admin configures those afterward through the ordinary Device Edit/Fleet UI, same as any freshly-registered real device.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost("Device")]
        public async Task<ActionResult<DeviceDto>> CreateVirtualDevice()
        {
            string? callerName = User.Identity?.Name;
            if (string.IsNullOrEmpty(callerName))
            {
                return Unauthorized();
            }
            User? caller = await users.UserGetAsync(null, callerName, null);
            if (caller?.IDUser is not int callerUserId)
            {
                return NotFound();
            }

            string pin = AuthenticationProvider.GetPin();
            ServerConfig serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            await users.UserSetDevicePinAsync(callerUserId, pin, DateTime.UtcNow.AddMinutes(serverConfig.DevicePinValidMinutes));

            // "02" is a locally-administered MAC prefix (IEEE 802-2014 sec 8.2.2) - guaranteed to never collide with a real vendor OUI a physical device might report.
            string mac = "02" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

            var client = new VirtualDeviceClient(httpClientFactory.CreateClient(BackgroundWorkers.VirtualDeviceRunnerBackgroundService.HttpClientName));
            DeviceConfig? config = await client.RegisterAsync(mac, callerName, pin, displayName: null);
            if (config?.deviceID is not int deviceId)
            {
                return StatusCode(502, "Virtual device registration did not return a device id.");
            }

            await simulationRepo.VirtualDeviceRegisterAsync(deviceId);
            // ApiId/ApiKey stay internal - same rule as every other device-facing GET, never returned to an admin caller (DeviceDto has no property for either).
            Device? created = await deviceRepo.DeviceGetByIdAsync(deviceId);
            return Ok(created?.ToDto());
        }

        /// Tenant-scoped for everyone including Global admin - a deliberate deviation from the usual Global-admin-sees-everything pattern, since a simulation is scoped to the tenant it was created for.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpGet("Device")]
        public async Task<ActionResult<IList<int>>> ListVirtualDevices() =>
            Ok(await simulationRepo.VirtualDeviceIdsGetAsync(CallerTenantId));

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpDelete("Device/{idDevice}")]
        public async Task<ActionResult> DeleteVirtualDevice(int idDevice)
        {
            Device? device = await deviceRepo.DeviceGetByIdAsync(idDevice);
            if (device is null)
            {
                return NotFound();
            }
            if (!CallerManagesDevices(device.TenantID))
            {
                return StatusCode(403, "Device belongs to a different tenant");
            }

            await simulationRepo.VirtualDeviceDeleteAsync(idDevice, device.TenantID);
            return Ok();
        }

        // ---- Simulation sessions (roadmap #403) - "Add Simulation" is now the entry point; devices join a named, time-boxed session instead of being independently toggled. ----

        /// Hard cap regardless of preset/custom entry - SimulationSessionExpiryEvaluator's own safety net only works if no session can ever be created past this.
        private const int MaxDurationMinutes = 48 * 60;

        /// Name only now; devices are added and the session is started as separate later steps (StartSession below), not bundled into creation.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost("Session")]
        public async Task<ActionResult<SimulationSession>> CreateSession([FromBody] SimulationSessionCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required.");
            }
            SimulationSession created;
            try
            {
                created = await simulationRepo.SimulationSessionAddAsync(new SimulationSession
                {
                    TenantID = CallerTenantId ?? 0,
                    Name = request.Name.Trim(),
                }, () => quotaEnforcer.CheckCanAddSimulationAsync(CallerTenantId));
            }
            catch (QuotaLimitExceededException ex)
            {
                return StatusCode(403, ex.Message);
            }
            await WriteAuditAsync("Simulation.SessionCreated", created.TenantID, "SimulationSession", created.IDSimulationSession.ToString()!, created.Name);
            return Ok(created);
        }

        /// Tenant-scoped for everyone including Global admin, same deliberate deviation as ListVirtualDevices above - a session belongs to the tenant it was created for.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpGet("Session")]
        public async Task<ActionResult<IList<SimulationSession>>> ListSessions() =>
            Ok(await simulationRepo.SimulationSessionsGetAsync(CallerTenantId));

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpGet("Session/{idSimulationSession}")]
        public async Task<ActionResult<SimulationSession>> GetSession(int idSimulationSession)
        {
            SimulationSession? session = await simulationRepo.SimulationSessionGetByIdAsync(idSimulationSession);
            if (session is null)
            {
                return NotFound();
            }
            if (session.TenantID != CallerTenantId && !CallerManagesUsersGlobally)
            {
                return StatusCode(403, "Session belongs to a different tenant");
            }
            return Ok(session);
        }

        /// Starts a never-started session, or resumes one that was previously Stopped/expired; rejected if the session is CURRENTLY running (Stop it first). Devices already added stay added - this only sets the time window.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost("Session/{idSimulationSession}/Start")]
        public async Task<ActionResult> StartSession(int idSimulationSession, [FromBody] SimulationSessionStartRequest request)
        {
            if (request.DurationMinutes is < 1 or > MaxDurationMinutes)
            {
                return BadRequest($"Duration must be between 1 and {MaxDurationMinutes} minutes (48 hours).");
            }
            SimulationSession? session = await simulationRepo.SimulationSessionGetByIdAsync(idSimulationSession);
            if (session is null)
            {
                return NotFound();
            }
            if (session.TenantID != CallerTenantId && !CallerManagesUsersGlobally)
            {
                return StatusCode(403, "Session belongs to a different tenant");
            }
            bool isRunning = session.StartedAtUtc != null && session.StoppedAtUtc == null && session.ExpiresAtUtc > DateTimeOffset.UtcNow;
            if (isRunning)
            {
                return Conflict("This session is already running - stop it first.");
            }

            await simulationRepo.SimulationSessionStartAsync(idSimulationSession, request.DurationMinutes);
            await WriteAuditAsync("Simulation.SessionStarted", session.TenantID, "SimulationSession", idSimulationSession.ToString(), session.Name);
            return Ok();
        }

        /// Explicit early stop - also turns off every member physical device's sensor override, same cleanup SimulationSessionExpiryEvaluator does on a natural 48h expiry.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost("Session/{idSimulationSession}/Stop")]
        public async Task<ActionResult> StopSession(int idSimulationSession)
        {
            SimulationSession? session = await simulationRepo.SimulationSessionGetByIdAsync(idSimulationSession);
            if (session is null)
            {
                return NotFound();
            }
            if (session.TenantID != CallerTenantId && !CallerManagesUsersGlobally)
            {
                return StatusCode(403, "Session belongs to a different tenant");
            }

            IList<int> virtualIds = await simulationRepo.VirtualDeviceIdsGetAsync(session.TenantID);
            foreach (DeviceDto member in session.Devices)
            {
                if (!virtualIds.Contains(member.IDDevice!.Value))
                {
                    await deviceRepo.DeviceSimulationSetAsync(member.IDDevice!.Value, new DeviceSimulation { Enabled = false });
                }
            }
            await simulationRepo.SimulationSessionStopAsync(idSimulationSession);
            await WriteAuditAsync("Simulation.SessionStopped", session.TenantID, "SimulationSession", idSimulationSession.ToString(), session.Name);
            return Ok();
        }

        /// Same physical-override cleanup as StopSession first (a running session must never leave a device stuck simulating just because its session was deleted), then hard-removes the session and its device memberships. A virtual device that was only IN this session is left as-is (still exists, just no longer in an active session) - deleting it entirely is the separate, explicit DeleteVirtualDevice action.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpDelete("Session/{idSimulationSession}")]
        public async Task<ActionResult> DeleteSession(int idSimulationSession)
        {
            SimulationSession? session = await simulationRepo.SimulationSessionGetByIdAsync(idSimulationSession);
            if (session is null)
            {
                return NotFound();
            }
            if (session.TenantID != CallerTenantId && !CallerManagesUsersGlobally)
            {
                return StatusCode(403, "Session belongs to a different tenant");
            }

            IList<int> virtualIds = await simulationRepo.VirtualDeviceIdsGetAsync(session.TenantID);
            foreach (DeviceDto member in session.Devices)
            {
                if (!virtualIds.Contains(member.IDDevice!.Value))
                {
                    await deviceRepo.DeviceSimulationSetAsync(member.IDDevice!.Value, new DeviceSimulation { Enabled = false });
                }
            }
            await simulationRepo.SimulationSessionDeleteAsync(idSimulationSession);
            await WriteAuditAsync("Simulation.SessionDeleted", session.TenantID, "SimulationSession", idSimulationSession.ToString(), session.Name);
            return Ok();
        }

        /// Adds an existing (physical or virtual) device to the session - a physical device also gets its sensor-override flag turned on here (Enabled=true, no metric values yet; the admin sets those afterward through the existing per-device Simulation form). A device already active in a DIFFERENT session is rejected, not silently moved.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost("Session/{idSimulationSession}/Device/{idDevice}")]
        public async Task<ActionResult> AddDeviceToSession(int idSimulationSession, int idDevice)
        {
            SimulationSession? session = await simulationRepo.SimulationSessionGetByIdAsync(idSimulationSession);
            if (session is null)
            {
                return NotFound("Session not found.");
            }
            if (session.TenantID != CallerTenantId && !CallerManagesUsersGlobally)
            {
                return StatusCode(403, "Session belongs to a different tenant");
            }
            if (session.StoppedAtUtc != null || session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                return BadRequest("This session has already ended.");
            }
            Device? device = await deviceRepo.DeviceGetByIdAsync(idDevice);
            if (device is null)
            {
                return NotFound("Device not found.");
            }
            if (!CallerManagesDevices(device.TenantID))
            {
                return StatusCode(403, "Device belongs to a different tenant");
            }

            bool added = await simulationRepo.SimulationSessionDeviceAddAsync(idSimulationSession, idDevice);
            if (!added)
            {
                int? busyWith = await simulationRepo.DeviceActiveSimulationSessionIdGetAsync(idDevice);
                return Conflict($"Device is already active in a different simulation session (id {busyWith}).");
            }

            bool isVirtual = (await simulationRepo.VirtualDeviceIdsGetAsync(device.TenantID)).Contains(idDevice);
            if (!isVirtual)
            {
                DeviceSimulation existing = await deviceRepo.DeviceSimulationGetAsync(idDevice) ?? new DeviceSimulation();
                existing.Enabled = true;
                await deviceRepo.DeviceSimulationSetAsync(idDevice, existing);
            }
            return Ok();
        }

        /// Removes a device from the session early, without waiting for the whole session to end - a physical device's sensor override is turned off the same way StopSession does for every member.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpDelete("Session/{idSimulationSession}/Device/{idDevice}")]
        public async Task<ActionResult> RemoveDeviceFromSession(int idSimulationSession, int idDevice)
        {
            SimulationSession? session = await simulationRepo.SimulationSessionGetByIdAsync(idSimulationSession);
            if (session is null)
            {
                return NotFound();
            }
            if (session.TenantID != CallerTenantId && !CallerManagesUsersGlobally)
            {
                return StatusCode(403, "Session belongs to a different tenant");
            }

            await simulationRepo.SimulationSessionDeviceRemoveAsync(idSimulationSession, idDevice);
            bool isVirtual = (await simulationRepo.VirtualDeviceIdsGetAsync(session.TenantID)).Contains(idDevice);
            if (!isVirtual)
            {
                await deviceRepo.DeviceSimulationSetAsync(idDevice, new DeviceSimulation { Enabled = false });
            }
            return Ok();
        }

        // ---- Simulation-scoped rules - a member device evaluates these ahead of its real Zone>Unit>Farm>Global rules, falling back to that hierarchy for whatever a session has no rule for. ----

        /// Same ownership check every other Session-scoped route in this controller already does - kept local rather than shared since it's three lines and every one of these routes needs it inline anyway.
        private async Task<OwnedResult<SimulationSession>> EnsureOwnedSessionAsync(int idSimulationSession)
        {
            SimulationSession? session = await simulationRepo.SimulationSessionGetByIdAsync(idSimulationSession);
            if (session is null)
            {
                return (null, NotFound());
            }
            if (session.TenantID != CallerTenantId && !CallerManagesUsersGlobally)
            {
                return (null, StatusCode(403, "Session belongs to a different tenant"));
            }
            return (session, null);
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpGet("Session/{idSimulationSession}/Rule")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneRule>>> SessionRulesGet(int idSimulationSession)
        {
            var (session, error) = await EnsureOwnedSessionAsync(idSimulationSession);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.RulesGetForSimulationAsync(session!.IDSimulationSession!.Value));
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost("Session/{idSimulationSession}/Rule")]
        public async Task<ActionResult<int>> SessionRuleAdd(int idSimulationSession, [FromBody] DeviceFarmUnitZoneRule rule)
        {
            var (session, error) = await EnsureOwnedSessionAsync(idSimulationSession);
            if (error != null)
            {
                return error;
            }
            if (session!.StoppedAtUtc != null || session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                return BadRequest("This session has already ended.");
            }

            rule.DeviceFarmUnitZoneID = null;
            rule.DeviceFarmUnitID = null;
            rule.DeviceFarmID = null;
            rule.ExperimentID = null;
            rule.SimulationSessionID = idSimulationSession;
            rule.TenantID = session.TenantID ?? CallerTenantId ?? 0;

            if (await ruleValidation.ShapeErrorAsync(rule) is string shapeError)
            {
                return BadRequest(shapeError);
            }
            int existingCount = (await deviceFarmUnitRepo.RulesGetForSimulationAsync(idSimulationSession)).Count;
            int configuredMax = (await serverConfigRepo.ServerConfigGetAsync(1)).MaxRulesPerZone ?? settings.MaxRulesPerZone;
            int effectiveMax = Math.Min(configuredMax, HardMaxRulesPerZone);
            if (existingCount >= effectiveMax)
            {
                return BadRequest($"This session already has {existingCount} rules, the configured maximum ({effectiveMax}). Remove one before adding another.");
            }

            int idRule = await deviceFarmUnitRepo.RuleAddAsync(rule);
            await WriteAuditAsync("DeviceFarmUnitZoneRule.Created", rule.TenantID, "DeviceFarmUnitZoneRule", idRule.ToString(), $"simulation session {idSimulationSession}, {rule.ActionType}/{rule.RelayFunction} \"{rule.Name}\"");
            return Ok(idRule);
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpDelete("Session/{idSimulationSession}/Rule/{idRule}")]
        public async Task<ActionResult<bool>> SessionRuleDelete(int idSimulationSession, int idRule)
        {
            var (session, error) = await EnsureOwnedSessionAsync(idSimulationSession);
            if (error != null)
            {
                return error;
            }
            DeviceFarmUnitZoneRule? rule = await deviceFarmUnitRepo.RuleGetByIdAsync(idRule);
            if (rule == null || rule.SimulationSessionID != idSimulationSession)
            {
                return NotFound();
            }

            var referencing = await deviceFarmUnitRepo.RulesReferencingAsync(idRule, rule.TenantID);
            if (referencing.Count > 0)
            {
                string names = string.Join(", ", referencing.Select(r => $"#{r.IDDeviceFarmUnitZoneRule}"));
                return Conflict($"Cannot delete: still referenced by another rule's \"another rule fired\" condition ({names}). Remove that condition first.");
            }

            await deviceFarmUnitRepo.RuleDeleteAsync(idRule);
            await WriteAuditAsync("DeviceFarmUnitZoneRule.Deleted", rule.TenantID, "DeviceFarmUnitZoneRule", idRule.ToString(), $"simulation session {idSimulationSession}, {rule.ActionType}/{rule.RelayFunction} \"{rule.Name}\"");
            return true;
        }

        // ---- Simulation groups - a whole Unit/Zone added together, one override value set fanned out to every member device. ----

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpGet("Session/{idSimulationSession}/Group")]
        public async Task<ActionResult<IList<SimulationGroup>>> SessionGroupsGet(int idSimulationSession)
        {
            var (session, error) = await EnsureOwnedSessionAsync(idSimulationSession);
            if (error != null)
            {
                return error;
            }
            return Ok(await simulationRepo.SimulationGroupsGetAsync(session!.IDSimulationSession!.Value));
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost("Session/{idSimulationSession}/Group")]
        public async Task<ActionResult<SimulationGroup>> SessionGroupAdd(int idSimulationSession, [FromBody] SimulationGroup group)
        {
            var (session, error) = await EnsureOwnedSessionAsync(idSimulationSession);
            if (error != null)
            {
                return error;
            }
            if (session!.StoppedAtUtc != null || session.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                return BadRequest("This session has already ended.");
            }

            ActionResult? scopeError = group.Scope switch
            {
                SimulationGroupScope.Unit => (await EnsureOwnedUnitAsync(group.ScopeID)).Error,
                SimulationGroupScope.Zone => (await EnsureOwnedZoneAsync(group.ScopeID)).Error,
                _ => BadRequest("Unknown group scope."),
            };
            if (scopeError != null)
            {
                return scopeError;
            }

            group.IDSimulationGroup = null;
            group.IDSimulationSession = idSimulationSession;
            SimulationGroup created = await simulationRepo.SimulationGroupAddAsync(group);
            await WriteAuditAsync("Simulation.GroupAdded", session.TenantID, "SimulationGroup", created.IDSimulationGroup.ToString()!, $"session {idSimulationSession}, {group.Scope} {group.ScopeID} ({created.MemberDeviceCount} device(s))");
            return Ok(created);
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPut("Session/{idSimulationSession}/Group/{idGroup}")]
        public async Task<ActionResult> SessionGroupUpdate(int idSimulationSession, int idGroup, [FromBody] SimulationGroup group)
        {
            var (session, error) = await EnsureOwnedSessionAsync(idSimulationSession);
            if (error != null)
            {
                return error;
            }
            SimulationGroup? existing = await simulationRepo.SimulationGroupGetByIdAsync(idGroup);
            if (existing == null || existing.IDSimulationSession != idSimulationSession)
            {
                return NotFound();
            }

            group.IDSimulationGroup = idGroup;
            group.IDSimulationSession = idSimulationSession;
            group.Scope = existing.Scope;
            group.ScopeID = existing.ScopeID;
            await simulationRepo.SimulationGroupUpdateAsync(group);
            await WriteAuditAsync("Simulation.GroupUpdated", session!.TenantID, "SimulationGroup", idGroup.ToString(), $"session {idSimulationSession}, {existing.Scope} {existing.ScopeID}");
            return Ok();
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpDelete("Session/{idSimulationSession}/Group/{idGroup}")]
        public async Task<ActionResult> SessionGroupDelete(int idSimulationSession, int idGroup)
        {
            var (session, error) = await EnsureOwnedSessionAsync(idSimulationSession);
            if (error != null)
            {
                return error;
            }
            SimulationGroup? existing = await simulationRepo.SimulationGroupGetByIdAsync(idGroup);
            if (existing == null || existing.IDSimulationSession != idSimulationSession)
            {
                return NotFound();
            }

            await simulationRepo.SimulationGroupDeleteAsync(idGroup);
            await WriteAuditAsync("Simulation.GroupDeleted", session!.TenantID, "SimulationGroup", idGroup.ToString(), $"session {idSimulationSession}, {existing.Scope} {existing.ScopeID}");
            return Ok();
        }

        /// Same shape as DeviceFarmUnitApiController's own EnsureOwnedUnitAsync/EnsureOwnedZoneAsync - kept local since this controller doesn't otherwise need the rest of that controller's ownership surface.
        private async Task<OwnedResult<DeviceFarmUnit>> EnsureOwnedUnitAsync(int idDeviceFarmUnit)
        {
            DeviceFarmUnit? unit = await deviceFarmUnitRepo.DeviceFarmUnitGetByIdAsync(idDeviceFarmUnit);
            if (unit == null)
            {
                return (null, NotFound("Unit not found."));
            }
            return unit.TenantID != CallerTenantId && !CallerManagesUsersGlobally
                ? (null, StatusCode(403, "Unit belongs to a different tenant"))
                : (unit, null);
        }

        private async Task<OwnedResult<DeviceFarmUnitZone>> EnsureOwnedZoneAsync(int idDeviceFarmUnitZone)
        {
            DeviceFarmUnitZone? zone = await deviceFarmUnitRepo.DeviceFarmUnitZoneGetByIdAsync(idDeviceFarmUnitZone);
            if (zone == null)
            {
                return (null, NotFound("Zone not found."));
            }
            return zone.TenantID != CallerTenantId && !CallerManagesUsersGlobally
                ? (null, StatusCode(403, "Zone belongs to a different tenant"))
                : (zone, null);
        }
    }
}
