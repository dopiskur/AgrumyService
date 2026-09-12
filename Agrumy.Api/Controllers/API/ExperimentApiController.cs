using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Devices;
using Agrumy.Shared;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Controllers.API
{
    /// Long-term real-device rule experiments: create/list/stop, an experiment-scoped rule set (evaluated ahead of the real Zone/Unit/Farm/Global hierarchy, same as Simulation's own rules), and a read-only view of the sensor/controller history dual-written while the experiment is active.
    [Route("/api/Experiment")]
    public class ExperimentApiController(IExperimentRepository experimentRepo, IDeviceFarmUnitRepository deviceFarmUnitRepo, IUserRepository userRepo, IAuditLogRepository auditLogRepo, IServerConfigRepository serverConfigRepo, ICache cache, RuleValidationService ruleValidation, IOptions<AgrumySettings> settingsOptions) : ApiControllerBase(userRepo, auditLogRepo, cache)
    {
        private readonly AgrumySettings settings = settingsOptions.Value;
        // Same ceiling every other rule-add route in the system uses (DeviceFarmUnitApiController's Zone/Unit/Farm/Global, SimulationApiController's Session) - one more scope, no reason for a different cap.
        private const int HardMaxRulesPerZone = 32;
        // Most-recent-first cap for the raw data view - a basic table, not a paged/exportable report (deferred to a future roadmap item).
        private const int MaxDataRowsReturned = 500;

        private async Task<OwnedResult<Experiment>> EnsureOwnedExperimentAsync(int idExperiment, bool forWrite = true)
        {
            Experiment? experiment = await experimentRepo.ExperimentGetByIdAsync(idExperiment);
            if (experiment is null)
            {
                return (null, NotFound());
            }
            bool crossTenantAllowed = CallerManagesUsersGlobally || (!forWrite && CallerHasRole(RoleNames.GlobalReader));
            if (experiment.TenantID != CallerTenantId && !crossTenantAllowed)
            {
                return (null, ForbidWith("Experiment belongs to a different tenant"));
            }
            return (experiment, null);
        }

        private async Task<ActionResult?> ScopeErrorAsync(HierarchyNodeKind scope, int scopeId)
        {
            switch (scope)
            {
                case HierarchyNodeKind.Farm:
                    DeviceFarm? farm = await deviceFarmUnitRepo.DeviceFarmGetByIdAsync(scopeId);
                    if (farm is null) { return NotFound("Farm not found."); }
                    return farm.TenantID != CallerTenantId && !CallerManagesUsersGlobally ? ForbidWith("Farm belongs to a different tenant") : null;
                case HierarchyNodeKind.Unit:
                    DeviceFarmUnit? unit = await deviceFarmUnitRepo.DeviceFarmUnitGetByIdAsync(scopeId);
                    if (unit is null) { return NotFound("Unit not found."); }
                    return unit.TenantID != CallerTenantId && !CallerManagesUsersGlobally ? ForbidWith("Unit belongs to a different tenant") : null;
                case HierarchyNodeKind.Zone:
                    DeviceFarmUnitZone? zone = await deviceFarmUnitRepo.DeviceFarmUnitZoneGetByIdAsync(scopeId);
                    if (zone is null) { return NotFound("Zone not found."); }
                    return zone.TenantID != CallerTenantId && !CallerManagesUsersGlobally ? ForbidWith("Zone belongs to a different tenant") : null;
                default:
                    return BadRequest("Unknown experiment scope.");
            }
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        public async Task<ActionResult<Experiment>> Create([FromBody] ExperimentCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Name is required.");
            }
            if (await ScopeErrorAsync(request.Scope, request.ScopeID) is ActionResult scopeError)
            {
                return scopeError;
            }

            Experiment created = await experimentRepo.ExperimentAddAsync(new Experiment
            {
                TenantID = CallerTenantId ?? 0,
                Name = request.Name.Trim(),
                Scope = request.Scope,
                ScopeID = request.ScopeID,
                ExpiresAtUtc = request.ExpiresAtUtc,
            });
            await WriteAuditAsync("Experiment.Created", created.TenantID, "Experiment", created.IDExperiment.ToString()!, $"{created.Scope} {created.ScopeID} \"{created.Name}\"");
            return Ok(created);
        }

        /// Tenant-scoped for everyone including Global admin, same deliberate deviation SimulationApiController's own session routes use - an experiment belongs to the tenant it was created for.
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet]
        public async Task<ActionResult<IList<Experiment>>> List() => Ok(await experimentRepo.ExperimentsGetAsync(CallerTenantId));

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("{idExperiment}")]
        public async Task<ActionResult<Experiment>> Get(int idExperiment)
        {
            var (experiment, error) = await EnsureOwnedExperimentAsync(idExperiment, forWrite: false);
            return error ?? Ok(experiment);
        }

        /// No hard cap/auto-expiry evaluator (unlike Simulation's 48h SimulationSessionExpiryEvaluator) - ExpiresAtUtc, when set, is honored lazily by ActiveExperimentIdForZoneAsync/ActiveExperimentIdsByZoneAsync instead, so an experiment simply stops applying once it passes, whether or not this Stop is ever called.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("{idExperiment}/Stop")]
        public async Task<ActionResult> Stop(int idExperiment)
        {
            var (experiment, error) = await EnsureOwnedExperimentAsync(idExperiment);
            if (error != null)
            {
                return error;
            }
            await experimentRepo.ExperimentStopAsync(idExperiment);
            await WriteAuditAsync("Experiment.Stopped", experiment!.TenantID, "Experiment", idExperiment.ToString(), experiment.Name);
            return Ok();
        }

        // ---- Experiment-scoped rules - a device in scope evaluates these ahead of its real Zone>Unit>Farm>Global rules, falling back to that hierarchy for anything the experiment has no rule for. ----

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("{idExperiment}/Rule")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneRule>>> RulesGet(int idExperiment)
        {
            var (experiment, error) = await EnsureOwnedExperimentAsync(idExperiment, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.RulesGetForExperimentAsync(experiment!.IDExperiment!.Value));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("{idExperiment}/Rule")]
        public async Task<ActionResult<int>> RuleAdd(int idExperiment, [FromBody] DeviceFarmUnitZoneRule rule)
        {
            var (experiment, error) = await EnsureOwnedExperimentAsync(idExperiment);
            if (error != null)
            {
                return error;
            }
            if (experiment!.StoppedAtUtc != null || (experiment.ExpiresAtUtc is DateTimeOffset expires && expires <= DateTimeOffset.UtcNow))
            {
                return BadRequest("This experiment has already ended.");
            }

            rule.DeviceFarmUnitZoneID = null;
            rule.DeviceFarmUnitID = null;
            rule.DeviceFarmID = null;
            rule.SimulationSessionID = null;
            rule.ExperimentID = idExperiment;
            rule.TenantID = experiment.TenantID ?? CallerTenantId ?? 0;

            if (await ruleValidation.ShapeErrorAsync(rule) is string shapeError)
            {
                return BadRequest(shapeError);
            }
            int existingCount = (await deviceFarmUnitRepo.RulesGetForExperimentAsync(idExperiment)).Count;
            int configuredMax = (await serverConfigRepo.ServerConfigGetAsync(1)).MaxRulesPerZone ?? settings.MaxRulesPerZone;
            int effectiveMax = Math.Min(configuredMax, HardMaxRulesPerZone);
            if (existingCount >= effectiveMax)
            {
                return BadRequest($"This experiment already has {existingCount} rules, the configured maximum ({effectiveMax}). Remove one before adding another.");
            }

            int idRule = await deviceFarmUnitRepo.RuleAddAsync(rule);
            await WriteAuditAsync("DeviceFarmUnitZoneRule.Created", rule.TenantID, "DeviceFarmUnitZoneRule", idRule.ToString(), $"experiment {idExperiment}, {rule.ActionType}/{rule.RelayFunction} \"{rule.Name}\"");
            return Ok(idRule);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("{idExperiment}/Rule/{idRule}")]
        public async Task<ActionResult<bool>> RuleDelete(int idExperiment, int idRule)
        {
            var (experiment, error) = await EnsureOwnedExperimentAsync(idExperiment);
            if (error != null)
            {
                return error;
            }
            DeviceFarmUnitZoneRule? rule = await deviceFarmUnitRepo.RuleGetByIdAsync(idRule);
            if (rule == null || rule.ExperimentID != idExperiment)
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
            await WriteAuditAsync("DeviceFarmUnitZoneRule.Deleted", rule.TenantID, "DeviceFarmUnitZoneRule", idRule.ToString(), $"experiment {idExperiment}, {rule.ActionType}/{rule.RelayFunction} \"{rule.Name}\"");
            return true;
        }

        // ---- Raw data view - basic, most-recent-first; charting/comparison analysis is a deferred follow-up. ----

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("{idExperiment}/SensorData")]
        public async Task<ActionResult<IList<ExperimentSensorSample>>> SensorDataGet(int idExperiment)
        {
            var (experiment, error) = await EnsureOwnedExperimentAsync(idExperiment, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await experimentRepo.ExperimentSensorSamplesGetAsync(idExperiment, MaxDataRowsReturned));
        }

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        [HttpGet("{idExperiment}/ControllerData")]
        public async Task<ActionResult<IList<ExperimentControllerEvent>>> ControllerDataGet(int idExperiment)
        {
            var (experiment, error) = await EnsureOwnedExperimentAsync(idExperiment, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await experimentRepo.ExperimentControllerEventsGetAsync(idExperiment, MaxDataRowsReturned));
        }
    }
}
