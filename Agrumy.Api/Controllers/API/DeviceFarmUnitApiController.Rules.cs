using Agrumy.Shared;
using Agrumy.Api.Commands;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Quota;
using Agrumy.Rules;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Controllers.API
{
    public partial class DeviceFarmUnitApiController
    {
        [Authorize]
        [HttpGet("Zone/Rule")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneRule>>> DeviceFarmUnitZoneRulesGet(int? idDeviceFarmUnitZone)
        {
            if (CallerIsDataReaderOnly)
            {
                return ForbidWith("Data Reader role cannot view zone rules.");
            }
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.RulesGetForZoneAsync(zone!.IDDeviceFarmUnitZone!.Value));
        }

        [Authorize]
        [HttpGet("Unit/Rule")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneRule>>> DeviceFarmUnitRulesGet(int? idDeviceFarmUnit)
        {
            if (CallerIsDataReaderOnly)
            {
                return ForbidWith("Data Reader role cannot view unit rules.");
            }
            var (unit, error) = await EnsureOwnedUnitAsync(idDeviceFarmUnit, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.RulesGetForUnitAsync(unit!.IDDeviceFarmUnit!.Value));
        }

        [Authorize]
        [HttpGet("Farm/Rule")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneRule>>> DeviceFarmRulesGet(int? idDeviceFarm)
        {
            if (CallerIsDataReaderOnly)
            {
                return ForbidWith("Data Reader role cannot view farm rules.");
            }
            var (farm, error) = await EnsureOwnedFarmAsync(idDeviceFarm, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.RulesGetForFarmAsync(farm!.IDDeviceFarm!.Value));
        }

        [Authorize]
        [HttpGet("Global/Rule")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneRule>>> GlobalRulesGet()
        {
            if (CallerIsDataReaderOnly)
            {
                return ForbidWith("Data Reader role cannot view global rules.");
            }
            if (CallerTenantId is not int tenantId)
            {
                return ForbidWith("Caller has no tenant.");
            }
            return Ok(await deviceFarmUnitRepo.RulesGetForTenantGlobalAsync(tenantId));
        }

        [Authorize]
        [HttpGet("Crop/Rule")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneRule>>> SowingRulesGet(int? idSowing)
        {
            if (CallerIsDataReaderOnly)
            {
                return ForbidWith("Data Reader role cannot view crop rules.");
            }
            var (crop, error) = await EnsureOwnedCropAsync(idSowing, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.RulesGetForSowingAsync(crop!.IDSowing!.Value));
        }

        [Authorize]
        [HttpGet("Parcel/Rule")]
        public async Task<ActionResult<IList<DeviceFarmUnitZoneRule>>> FarmParcelZoneRulesGet(int? idFarmParcelZone)
        {
            if (CallerIsDataReaderOnly)
            {
                return ForbidWith("Data Reader role cannot view parcel rules.");
            }
            var (parcel, error) = await EnsureOwnedParcelAsync(idFarmParcelZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            return Ok(await deviceFarmUnitRepo.RulesGetForFarmParcelZoneAsync(parcel!.IDFarmParcelZone!.Value));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone/Rule")]
        public async Task<ActionResult<RuleAddResult>> DeviceFarmUnitZoneRuleAdd([FromBody] DeviceFarmUnitZoneRule rule)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(rule.DeviceFarmUnitZoneID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            rule.DeviceFarmUnitID = null;
            rule.DeviceFarmID = null;
            rule.DeviceSowingID = null;
            rule.DeviceFarmParcelZoneID = null;
            rule.SimulationSessionID = null;
            rule.ExperimentID = null;
            rule.TenantID = zone!.TenantID ?? CallerTenantId ?? 0;
            return await AddRuleAsync(rule, existingCount: (await deviceFarmUnitRepo.RulesGetForZoneAsync(zone.IDDeviceFarmUnitZone!.Value)).Count, scopeLabel: $"zone {rule.DeviceFarmUnitZoneID}");
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Crop/Rule")]
        public async Task<ActionResult<RuleAddResult>> SowingRuleAdd([FromBody] DeviceFarmUnitZoneRule rule)
        {
            var (crop, error) = await EnsureOwnedCropAsync(rule.DeviceSowingID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            rule.DeviceFarmParcelZoneID = null;
            rule.DeviceFarmID = null;
            rule.SimulationSessionID = null;
            rule.ExperimentID = null;
            rule.TenantID = crop!.TenantID ?? CallerTenantId ?? 0;
            return await AddRuleAsync(rule, existingCount: (await deviceFarmUnitRepo.RulesGetForSowingAsync(crop.IDSowing!.Value)).Count, scopeLabel: $"crop {rule.DeviceSowingID}");
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Parcel/Rule")]
        public async Task<ActionResult<RuleAddResult>> FarmParcelZoneRuleAdd([FromBody] DeviceFarmUnitZoneRule rule)
        {
            var (parcel, error) = await EnsureOwnedParcelAsync(rule.DeviceFarmParcelZoneID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            rule.DeviceSowingID = null;
            rule.DeviceFarmID = null;
            rule.SimulationSessionID = null;
            rule.ExperimentID = null;
            rule.TenantID = parcel!.TenantID ?? CallerTenantId ?? 0;
            return await AddRuleAsync(rule, existingCount: (await deviceFarmUnitRepo.RulesGetForFarmParcelZoneAsync(parcel.IDFarmParcelZone!.Value)).Count, scopeLabel: $"parcel {rule.DeviceFarmParcelZoneID}");
        }

        /// Reuses AddRuleAsync per generated rule (same validation/cap-check/audit as adding one rule by hand) - a catalog entry's ranges are a starting point, not guaranteed to fit if the zone is already near its rule-count cap.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone/ApplyHorticultureCatalog")]
        public async Task<ActionResult<HorticultureCatalogApplyResult>> ApplyHorticultureCatalog(int? idDeviceFarmUnitZone, HorticultureCatalogType catalogType, int catalogId)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }

            HorticultureCatalogEntry? entry = await horticultureCatalogRepo.CatalogGetByIdAsync(catalogType, catalogId);
            if (entry == null)
            {
                return NotFound("Catalog entry not found.");
            }

            int zoneId = zone!.IDDeviceFarmUnitZone!.Value;
            IList<DeviceFarmUnitZoneRule> templateRules = HorticultureRuleTemplateBuilder.BuildRules(entry, zoneId);
            int existingCount = (await deviceFarmUnitRepo.RulesGetForZoneAsync(zoneId)).Count;
            int added = 0;
            var skipped = new List<string>();
            foreach (DeviceFarmUnitZoneRule rule in templateRules)
            {
                rule.TenantID = zone.TenantID ?? CallerTenantId ?? 0;
                ActionResult<RuleAddResult> result = await AddRuleAsync(rule, existingCount + added, scopeLabel: $"zone {zoneId}");
                if (result.Result is OkObjectResult)
                {
                    added++;
                }
                else
                {
                    skipped.Add(rule.Name);
                }
            }

            await WriteAuditAsync("DeviceFarmUnitZone.HorticultureCatalogApplied", zone.TenantID, "DeviceFarmUnitZone", zoneId.ToString(), $"{catalogType}/{entry.Name}: {added} rule(s) added");
            return Ok(new HorticultureCatalogApplyResult { RulesAdded = added, RulesSkipped = skipped });
        }

        /// "Day/Night targets" preset, same AddRuleAsync reuse as ApplyHorticultureCatalog. Rejects Screen/Vent (positional, no plain on/off threshold) and a day window that isn't a proper subset of the day (0 &lt;= start &lt; end &lt;= 86400, and not the whole day - a full-day "day" window leaves no room for a night rule to ever fire).
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone/ApplyDayNightPreset")]
        public async Task<ActionResult<DayNightPresetApplyResult>> ApplyDayNightPreset(int? idDeviceFarmUnitZone, [FromBody] DayNightTargetPresetRequest request)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            if (request.Function.IsPositional())
            {
                return BadRequest("Day/Night targets only apply to a plain on/off function, not Screen/Vent.");
            }
            if (request.Operator == ComparisonOperator.Between)
            {
                return BadRequest("Day/Night targets take one threshold per period, not a Between range.");
            }
            if (request.DayStartSeconds < 0 || request.DayEndSeconds > 86400 || request.DayStartSeconds >= request.DayEndSeconds
                || (request.DayStartSeconds == 0 && request.DayEndSeconds == 86400))
            {
                return BadRequest("Day window must start before it ends, both within one day, and leave room for a night window.");
            }
            if (string.IsNullOrWhiteSpace(request.NamePrefix))
            {
                return BadRequest("A name prefix is required.");
            }

            int zoneId = zone!.IDDeviceFarmUnitZone!.Value;
            (DeviceFarmUnitZoneRule day, DeviceFarmUnitZoneRule night) = DayNightTargetPresetBuilder.BuildRules(
                zoneId, request.Function, request.Metric, request.Operator, request.DayValue, request.NightValue, request.Hysteresis,
                request.DayStartSeconds, request.DayEndSeconds, request.NamePrefix.Trim());

            int tenantId = zone.TenantID ?? CallerTenantId ?? 0;
            day.TenantID = tenantId;
            night.TenantID = tenantId;
            int existingCount = (await deviceFarmUnitRepo.RulesGetForZoneAsync(zoneId)).Count;
            int added = 0;
            var skipped = new List<string>();
            foreach (DeviceFarmUnitZoneRule rule in new[] { day, night })
            {
                ActionResult<RuleAddResult> result = await AddRuleAsync(rule, existingCount + added, scopeLabel: $"zone {zoneId}");
                if (result.Result is OkObjectResult)
                {
                    added++;
                }
                else
                {
                    skipped.Add(rule.Name);
                }
            }

            await WriteAuditAsync("DeviceFarmUnitZone.DayNightPresetApplied", tenantId, "DeviceFarmUnitZone", zoneId.ToString(), $"{request.Function}/{request.NamePrefix}: {added} rule(s) added");
            return Ok(new DayNightPresetApplyResult { RulesAdded = added, RulesSkipped = skipped });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Unit/Rule")]
        public async Task<ActionResult<RuleAddResult>> DeviceFarmUnitRuleAdd([FromBody] DeviceFarmUnitZoneRule rule)
        {
            var (unit, error) = await EnsureOwnedUnitAsync(rule.DeviceFarmUnitID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            rule.DeviceFarmUnitZoneID = null;
            rule.DeviceFarmID = null;
            rule.DeviceSowingID = null;
            rule.DeviceFarmParcelZoneID = null;
            rule.SimulationSessionID = null;
            rule.ExperimentID = null;
            rule.TenantID = unit!.TenantID ?? CallerTenantId ?? 0;
            return await AddRuleAsync(rule, existingCount: (await deviceFarmUnitRepo.RulesGetForUnitAsync(unit.IDDeviceFarmUnit!.Value)).Count, scopeLabel: $"unit {rule.DeviceFarmUnitID}");
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Farm/Rule")]
        public async Task<ActionResult<RuleAddResult>> DeviceFarmRuleAdd([FromBody] DeviceFarmUnitZoneRule rule)
        {
            var (farm, error) = await EnsureOwnedFarmAsync(rule.DeviceFarmID, forWrite: true);
            if (error != null)
            {
                return error;
            }
            rule.DeviceFarmUnitZoneID = null;
            rule.DeviceFarmUnitID = null;
            rule.DeviceSowingID = null;
            rule.DeviceFarmParcelZoneID = null;
            rule.SimulationSessionID = null;
            rule.ExperimentID = null;
            rule.TenantID = farm!.TenantID ?? CallerTenantId ?? 0;
            return await AddRuleAsync(rule, existingCount: (await deviceFarmUnitRepo.RulesGetForFarmAsync(farm.IDDeviceFarm!.Value)).Count, scopeLabel: $"farm {rule.DeviceFarmID}");
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Global/Rule")]
        public async Task<ActionResult<RuleAddResult>> GlobalRuleAdd([FromBody] DeviceFarmUnitZoneRule rule)
        {
            if (CallerTenantId is not int tenantId)
            {
                return ForbidWith("Caller has no tenant.");
            }
            rule.DeviceFarmUnitZoneID = null;
            rule.DeviceFarmUnitID = null;
            rule.DeviceFarmID = null;
            rule.DeviceSowingID = null;
            rule.DeviceFarmParcelZoneID = null;
            rule.SimulationSessionID = null;
            rule.ExperimentID = null;
            rule.TenantID = tenantId;
            return await AddRuleAsync(rule, existingCount: (await deviceFarmUnitRepo.RulesGetForTenantGlobalAsync(tenantId)).Count, scopeLabel: $"tenant {tenantId} (global)");
        }

        /// Shared validate+cap+persist body for all four scopes - the only difference between them is which EnsureOwned*/existing-count call the caller already made.
        private async Task<ActionResult<RuleAddResult>> AddRuleAsync(DeviceFarmUnitZoneRule rule, int existingCount, string scopeLabel)
        {
            if (await ruleValidation.ShapeErrorAsync(rule) is string shapeError)
            {
                return BadRequest(shapeError);
            }
            int configuredMax = (await serverConfigRepo.ServerConfigGetAsync(1)).MaxRulesPerZone ?? settings.MaxRulesPerZone;
            int effectiveMax = Math.Min(configuredMax, HardMaxRulesPerZone);
            if (existingCount >= effectiveMax)
            {
                return BadRequest($"This scope already has {existingCount} rules, the configured maximum ({effectiveMax}). Remove one before adding another.");
            }
            string? conflictWarning = await ruleScopeConflict.FindConflictWarningAsync(rule);
            int idRule = await deviceFarmUnitRepo.RuleAddAsync(rule);
            await WriteAuditAsync("DeviceFarmUnitZoneRule.Created", rule.TenantID, "DeviceFarmUnitZoneRule", idRule.ToString(), $"{scopeLabel}, {rule.ActionType}/{rule.RelayFunction} \"{rule.Name}\"");
            return Ok(new RuleAddResult { IDDeviceFarmUnitZoneRule = idRule, ScopeConflictWarning = conflictWarning });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Zone/Rule")]
        public Task<ActionResult<bool>> DeviceFarmUnitZoneRuleDelete(int? idDeviceFarmUnitZoneRule) => DeleteRuleAsync(idDeviceFarmUnitZoneRule);

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Unit/Rule")]
        public Task<ActionResult<bool>> DeviceFarmUnitRuleDelete(int? idDeviceFarmUnitZoneRule) => DeleteRuleAsync(idDeviceFarmUnitZoneRule);

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Farm/Rule")]
        public Task<ActionResult<bool>> DeviceFarmRuleDelete(int? idDeviceFarmUnitZoneRule) => DeleteRuleAsync(idDeviceFarmUnitZoneRule);

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Global/Rule")]
        public Task<ActionResult<bool>> GlobalRuleDelete(int? idDeviceFarmUnitZoneRule) => DeleteRuleAsync(idDeviceFarmUnitZoneRule);

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Crop/Rule")]
        public Task<ActionResult<bool>> SowingRuleDelete(int? idDeviceFarmUnitZoneRule) => DeleteRuleAsync(idDeviceFarmUnitZoneRule);

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpDelete("Parcel/Rule")]
        public Task<ActionResult<bool>> FarmParcelZoneRuleDelete(int? idDeviceFarmUnitZoneRule) => DeleteRuleAsync(idDeviceFarmUnitZoneRule);

        /// One shared delete body regardless of which scope route it came in through - ownership is resolved from the rule's OWN scope fields, not the route.
        private async Task<ActionResult<bool>> DeleteRuleAsync(int? idRule)
        {
            DeviceFarmUnitZoneRule? rule = await deviceFarmUnitRepo.RuleGetByIdAsync(idRule);
            if (rule == null)
            {
                return NotFound();
            }
            var error = await EnsureOwnedRuleAsync(rule, forWrite: true);
            if (error != null)
            {
                return error;
            }
            var referencing = await deviceFarmUnitRepo.RulesReferencingAsync(idRule!.Value, rule.TenantID);
            if (referencing.Count > 0)
            {
                string names = string.Join(", ", referencing.Select(r => $"#{r.IDDeviceFarmUnitZoneRule}"));
                return Conflict($"Cannot delete: still referenced by another rule's \"another rule fired\" condition ({names}). Remove that condition first.");
            }
            await deviceFarmUnitRepo.RuleDeleteAsync(idRule.Value);
            await WriteAuditAsync("DeviceFarmUnitZoneRule.Deleted", rule.TenantID, "DeviceFarmUnitZoneRule", idRule.ToString()!, $"{rule.ActionType}/{rule.RelayFunction} \"{rule.Name}\"");
            return true;
        }

        /// Resolves ownership from the rule's OWN scope (Zone/Unit/Farm/Global), not the caller's route - a rule can only ever have one of those four shapes.
        private async Task<ActionResult?> EnsureOwnedRuleAsync(DeviceFarmUnitZoneRule rule, bool forWrite)
        {
            if (rule.DeviceFarmUnitZoneID is int idZone)
            {
                return (await EnsureOwnedZoneAsync(idZone, forWrite)).Error;
            }
            if (rule.DeviceFarmUnitID is int idUnit)
            {
                return (await EnsureOwnedUnitAsync(idUnit, forWrite)).Error;
            }
            if (rule.DeviceFarmID is int idFarm)
            {
                return (await EnsureOwnedFarmAsync(idFarm, forWrite)).Error;
            }
            if (rule.DeviceFarmParcelZoneID is int idParcel)
            {
                return (await EnsureOwnedParcelAsync(idParcel, forWrite)).Error;
            }
            if (rule.DeviceSowingID is int idCrop)
            {
                return (await EnsureOwnedCropAsync(idCrop, forWrite)).Error;
            }
            bool crossTenantAllowed = forWrite ? CallerManagesDevicesGlobally : CallerReadsDevicesGlobally;
            return rule.TenantID != CallerTenantId && !crossTenantAllowed
                ? ForbidWith("Rule belongs to a different tenant")
                : null;
        }

        // Shape+bound validation itself lives in RuleValidationService now (shared with SimulationApiController's Simulation-scoped rule routes).



        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone/ManualActuate")]
        public async Task<ActionResult<IReadOnlyList<int>>> ZoneManualActuateStart(int idDeviceFarmUnitZone, [FromBody] ManualActuateRequest request)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            ManualActuateResult result = await manualActuate.StartForZoneAsync(idDeviceFarmUnitZone, request);
            if (result.Outcome == ManualActuateOutcome.Success)
            {
                await WriteAuditAsync("DeviceFarmUnitZone.ManualActuateStarted", zone!.TenantID, "DeviceFarmUnitZone", idDeviceFarmUnitZone.ToString(), $"{request.RelayFunction}/{request.Mode}");
            }
            return ManualActuateResponse(result);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Unit/ManualActuate")]
        public async Task<ActionResult<IReadOnlyList<int>>> UnitManualActuateStart(int idDeviceFarmUnit, [FromBody] ManualActuateRequest request)
        {
            var (unit, error) = await EnsureOwnedUnitAsync(idDeviceFarmUnit, forWrite: true);
            if (error != null)
            {
                return error;
            }
            ManualActuateResult result = await manualActuate.StartForUnitAsync(idDeviceFarmUnit, request);
            if (result.Outcome == ManualActuateOutcome.Success)
            {
                await WriteAuditAsync("DeviceFarmUnit.ManualActuateStarted", unit!.TenantID, "DeviceFarmUnit", idDeviceFarmUnit.ToString(), $"{request.RelayFunction}/{request.Mode}");
            }
            return ManualActuateResponse(result);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Farm/ManualActuate")]
        public async Task<ActionResult<IReadOnlyList<int>>> DeviceFarmManualActuateStart(int idDeviceFarm, [FromBody] ManualActuateRequest request)
        {
            var (farm, error) = await EnsureOwnedFarmAsync(idDeviceFarm, forWrite: true);
            if (error != null)
            {
                return error;
            }
            ManualActuateResult result = await manualActuate.StartForFarmAsync(idDeviceFarm, request);
            if (result.Outcome == ManualActuateOutcome.Success)
            {
                await WriteAuditAsync("DeviceFarm.ManualActuateStarted", farm!.TenantID, "DeviceFarm", idDeviceFarm.ToString(), $"{request.RelayFunction}/{request.Mode}");
            }
            return ManualActuateResponse(result);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost("Zone/ManualActuate/Stop")]
        public async Task<ActionResult> ZoneManualActuateStop(int idDeviceFarmUnitZone, RelayFunction relayFunction)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: true);
            if (error != null)
            {
                return error;
            }
            await manualActuate.StopAsync(idDeviceFarmUnitZone, relayFunction);
            await WriteAuditAsync("DeviceFarmUnitZone.ManualActuateStopped", zone!.TenantID, "DeviceFarmUnitZone", idDeviceFarmUnitZone.ToString(), relayFunction.ToString());
            return Ok();
        }

        /// The zone's currently-active manual commands (not yet past ExpiresAtUtc) - what the Web UI polls to render "currently active, X remaining".
        [Authorize]
        [HttpGet("Zone/ManualActuate")]
        public async Task<ActionResult<IList<DeviceManualOverride>>> ZoneManualActuateStatus(int idDeviceFarmUnitZone)
        {
            var (zone, error) = await EnsureOwnedZoneAsync(idDeviceFarmUnitZone, forWrite: false);
            if (error != null)
            {
                return error;
            }
            Device? controller = await deviceFarmUnitRepo.DeviceFarmUnitZoneGetControllerAsync(idDeviceFarmUnitZone);
            if (controller?.IDDevice is not int deviceId)
            {
                return Ok(Array.Empty<DeviceManualOverride>());
            }
            return Ok(await deviceFarmUnitRepo.ManualOverridesActiveForDeviceAsync(deviceId));
        }

        private ActionResult<IReadOnlyList<int>> ManualActuateResponse(ManualActuateResult result) => result.Outcome switch
        {
            ManualActuateOutcome.Success => Ok(result.AffectedDeviceIds),
            ManualActuateOutcome.TargetNotFound => NotFound(result.Message),
            _ => BadRequest(result.Message),
        };
    }
}
