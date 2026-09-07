using api.Dal.Interface;
using api.Models;
using api.Security;
using api.Simulation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.API
{
    /// Admin-facing create/list/delete for fully virtual devices - the actual per-tick simulation runs in api.BackgroundWorkers.VirtualDeviceRunnerBackgroundService, not here.
    [Route("/api/Simulation")]
    public class SimulationApiController(ISimulationRepository simulationRepo, IDeviceRepository deviceRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, IServerConfigRepository serverConfigRepo, ICache cache, IHttpClientFactory httpClientFactory) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
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

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost("Session")]
        public async Task<ActionResult<SimulationSession>> CreateSession([FromBody] SimulationSessionCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required.");
            }
            if (request.DurationMinutes is < 1 or > MaxDurationMinutes)
            {
                return BadRequest($"Duration must be between 1 and {MaxDurationMinutes} minutes (48 hours).");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            SimulationSession created = await simulationRepo.SimulationSessionAddAsync(new SimulationSession
            {
                TenantID = CallerTenantId ?? 0,
                Name = request.Name.Trim(),
                StartedAtUtc = now,
                ExpiresAtUtc = now.AddMinutes(request.DurationMinutes),
            });
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
    }
}
