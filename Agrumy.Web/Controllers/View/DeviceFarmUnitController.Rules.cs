using Agrumy.Web.Security;
using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Shared.Utils;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    public partial class DeviceFarmUnitController
    {
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SafetyLimitsUpdate(int idDeviceFarmUnitZone, int? waterPumpMaxRunSeconds, int? waterPumpCooldownSeconds, bool skipWaterPumpWhenRainPredicted,
            int? heatingMaxRunSeconds, int? ventilationMaxRunSeconds, HeatingFailSafePolicyType? heatingFailSafePolicy)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            zone.WaterPumpMaxRunSeconds = waterPumpMaxRunSeconds;
            zone.WaterPumpCooldownSeconds = waterPumpCooldownSeconds;
            zone.SkipWaterPumpWhenRainPredicted = skipWaterPumpWhenRainPredicted;
            zone.HeatingMaxRunSeconds = heatingMaxRunSeconds;
            zone.VentilationMaxRunSeconds = ventilationMaxRunSeconds;
            zone.HeatingFailSafePolicy = heatingFailSafePolicy;
            try
            {
                await api.DeviceFarmUnitZoneUpdate(zone);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        // All three null together means "no tank tracking", the empty-string->null coercion below keeps a blank form submit from writing a zero-capacity/zero-calibration tank instead.
        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TankCalibrationUpdate(int idDeviceFarmUnitZone, double? tankCapacityLiters, int? waterLevelRawEmpty, int? waterLevelRawFull, double? waterPumpMinLevel)
        {
            DeviceFarmUnitZone zone = await api.DeviceFarmUnitZoneGetById(idDeviceFarmUnitZone);
            zone.TankCapacityLiters = tankCapacityLiters;
            zone.WaterLevelRawEmpty = waterLevelRawEmpty;
            zone.WaterLevelRawFull = waterLevelRawFull;
            zone.WaterPumpMinLevel = waterPumpMinLevel;
            try
            {
                await api.DeviceFarmUnitZoneUpdate(zone);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        // ---- Rules (Zone/Unit/Global scope) ----------------------------

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RuleAdd(int idDeviceFarmUnitZone, RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, idDeviceFarmUnitZone, null), r => api.DeviceFarmUnitZoneRuleAdd(r));
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RuleDelete(int idDeviceFarmUnitZoneRule, int idDeviceFarmUnitZone)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.DeviceFarmUnitZoneRuleDelete(r));
            return RedirectToAction(nameof(Zone), new { idDeviceFarmUnitZone });
        }

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> UnitRules(int idDeviceFarmUnit) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.Unit,
            ScopeId = idDeviceFarmUnit,
            Rules = await api.DeviceFarmUnitRulesGet(idDeviceFarmUnit),
            RedirectActionName = nameof(UnitRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitRuleAdd(int idDeviceFarmUnit, RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, null, idDeviceFarmUnit), r => api.DeviceFarmUnitRuleAdd(r));
            return RedirectToAction(nameof(UnitRules), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitRuleDelete(int idDeviceFarmUnitZoneRule, int idDeviceFarmUnit)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.DeviceFarmUnitRuleDelete(r));
            return RedirectToAction(nameof(UnitRules), new { idDeviceFarmUnit });
        }

        /// "Alert rules" - same Unit rule set as UnitRules, but the view (RuleEditorViewModel.ShowRelayCard) only shows the Notification card.
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> UnitAlertRules(int idDeviceFarmUnit) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.UnitAlert,
            ScopeId = idDeviceFarmUnit,
            Rules = await api.DeviceFarmUnitRulesGet(idDeviceFarmUnit),
            RedirectActionName = nameof(UnitAlertRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitAlertRuleAdd(int idDeviceFarmUnit, RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, null, idDeviceFarmUnit), r => api.DeviceFarmUnitRuleAdd(r));
            return RedirectToAction(nameof(UnitAlertRules), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UnitAlertRuleDelete(int idDeviceFarmUnitZoneRule, int idDeviceFarmUnit)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.DeviceFarmUnitRuleDelete(r));
            return RedirectToAction(nameof(UnitAlertRules), new { idDeviceFarmUnit });
        }

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> DeviceFarmRules(int idDeviceFarm) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.Farm,
            ScopeId = idDeviceFarm,
            Rules = await api.DeviceFarmRulesGet(idDeviceFarm),
            RedirectActionName = nameof(DeviceFarmRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeviceFarmRuleAdd(int idDeviceFarm, RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, null, null, idDeviceFarm), r => api.DeviceFarmRuleAdd(r));
            return RedirectToAction(nameof(DeviceFarmRules), new { idDeviceFarm });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeviceFarmRuleDelete(int idDeviceFarmUnitZoneRule, int idDeviceFarm)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.DeviceFarmRuleDelete(r));
            return RedirectToAction(nameof(DeviceFarmRules), new { idDeviceFarm });
        }

        /// "Alert rules" - same Farm rule set as DeviceFarmRules, Notification-only view like UnitAlertRules.
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> DeviceFarmAlertRules(int idDeviceFarm) => View(new RuleEditorViewModel
        {
            Scope = RuleScope.FarmAlert,
            ScopeId = idDeviceFarm,
            Rules = await api.DeviceFarmRulesGet(idDeviceFarm),
            RedirectActionName = nameof(DeviceFarmAlertRules),
        });

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeviceFarmAlertRuleAdd(int idDeviceFarm, RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, null, null, idDeviceFarm), r => api.DeviceFarmRuleAdd(r));
            return RedirectToAction(nameof(DeviceFarmAlertRules), new { idDeviceFarm });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeviceFarmAlertRuleDelete(int idDeviceFarmUnitZoneRule, int idDeviceFarm)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.DeviceFarmRuleDelete(r));
            return RedirectToAction(nameof(DeviceFarmAlertRules), new { idDeviceFarm });
        }

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> GlobalRules() => View(new GlobalRulesPageViewModel
        {
            Editor = new RuleEditorViewModel
            {
                Scope = RuleScope.Global,
                Rules = await api.GlobalRulesGet(),
                RedirectActionName = nameof(GlobalRules),
            },
            AllRules = await BuildRuleOverviewAsync(),
        });

        /// Flattens every scope's rules into one organization-wide list for GlobalRules' overview table - N+1 by design (one page load, not a hot path), same tradeoff as BuildZoneOptionsAsync above.
        private async Task<IList<RuleOverviewRow>> BuildRuleOverviewAsync()
        {
            var rows = new List<RuleOverviewRow>();
            foreach (DeviceFarmUnitZoneRule rule in await api.GlobalRulesGet())
            {
                rows.Add(new RuleOverviewRow { Rule = rule, ScopeLabel = "Global", DetailUrl = Url.Action(nameof(GlobalRules))! });
            }
            foreach (DeviceFarm farm in await api.DeviceFarmsGet())
            {
                if (farm.IDDeviceFarm is not int farmId) { continue; }
                foreach (DeviceFarmUnitZoneRule rule in await api.DeviceFarmRulesGet(farmId))
                {
                    rows.Add(new RuleOverviewRow { Rule = rule, ScopeLabel = $"Farm: {farm.DeviceFarmName}", DetailUrl = Url.Action(nameof(DeviceFarmRules), new { idDeviceFarm = farmId })! });
                }
            }
            foreach (DeviceFarmUnit unit in await api.DeviceFarmUnitsGet())
            {
                if (unit.IDDeviceFarmUnit is not int unitId) { continue; }
                foreach (DeviceFarmUnitZoneRule rule in await api.DeviceFarmUnitRulesGet(unitId))
                {
                    rows.Add(new RuleOverviewRow { Rule = rule, ScopeLabel = $"Unit: {unit.DeviceFarmUnitName}", DetailUrl = Url.Action(nameof(UnitRules), new { idDeviceFarmUnit = unitId })! });
                }
                foreach (DeviceFarmUnitZone zone in await api.DeviceFarmUnitZonesGet(unitId))
                {
                    if (zone.IDDeviceFarmUnitZone is not int zoneId) { continue; }
                    foreach (DeviceFarmUnitZoneRule rule in await api.DeviceFarmUnitZoneRulesGet(zoneId))
                    {
                        rows.Add(new RuleOverviewRow { Rule = rule, ScopeLabel = $"Zone: {zone.DeviceFarmUnitZoneName}", DetailUrl = Url.Action(nameof(Zone), new { idDeviceFarmUnitZone = zoneId })! });
                    }
                }
            }
            return rows;
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GlobalRuleAdd(RuleFormInput input)
        {
            await AddRuleAsync(BuildRule(input, null, null), r => api.GlobalRuleAdd(r));
            return RedirectToAction(nameof(GlobalRules));
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GlobalRuleDelete(int idDeviceFarmUnitZoneRule)
        {
            await DeleteRuleAsync(idDeviceFarmUnitZoneRule, r => api.GlobalRuleDelete(r));
            return RedirectToAction(nameof(GlobalRules));
        }

        private async Task AddRuleAsync(DeviceFarmUnitZoneRule rule, Func<DeviceFarmUnitZoneRule, Task<RuleAddResult>> add)
        {
            try
            {
                RuleAddResult result = await add(rule);
                if (result.ScopeConflictWarning != null)
                {
                    TempData["Warning"] = result.ScopeConflictWarning;
                }
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
        }

        private async Task DeleteRuleAsync(int idRule, Func<int?, Task> delete)
        {
            try
            {
                await delete(idRule);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
        }

        /// Builds a DeviceFarmUnitZoneRule from the form input - exactly one of idDeviceFarmUnitZone/idDeviceFarmUnit is non-null for Zone/Unit scope, both null for Global. RootConditionJson comes pre-built from wwwroot/js/rule-builder.js - must deserialize with ConditionConfigJson.Options, the options-less overload would misread the camelCase JS produced.
        private static DeviceFarmUnitZoneRule BuildRule(RuleFormInput input, int? idDeviceFarmUnitZone, int? idDeviceFarmUnit, int? idDeviceFarm = null)
        {
            ConditionNode? root = string.IsNullOrWhiteSpace(input.RootConditionJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<ConditionNode>(input.RootConditionJson, ConditionConfigJson.Options);
            return new DeviceFarmUnitZoneRule
            {
                DeviceFarmUnitZoneID = idDeviceFarmUnitZone,
                DeviceFarmUnitID = idDeviceFarmUnit,
                DeviceFarmID = idDeviceFarm,
                ActionType = input.ActionType,
                RelayFunction = input.ActionType == ActionType.Relay ? input.RelayFunction : null,
                Name = input.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
                TargetPercent = input.ActionType == ActionType.Relay ? input.TargetPercent : null,
                IsSafetyRule = input.IsSafetyRule,
                NotificationSubject = input.ActionType == ActionType.Notification ? input.NotificationSubject : null,
                NotificationBody = input.ActionType == ActionType.Notification ? input.NotificationBody : null,
                Root = root,
            };
        }
    }
}
