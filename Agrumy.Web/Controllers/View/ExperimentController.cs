using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Long-term real-device rule experiments; unlike Simulation there's no device-membership step (Scope+ScopeID alone defines who's in it) and no hard duration cap, so Create both names AND starts the experiment in one step.
    [Authorize]
    public class ExperimentController(IApi api) : Controller
    {
        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> Index()
        {
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            IList<DeviceFarmUnit> units = await api.DeviceFarmUnitsGet();
            var zones = new List<ZoneOption>();
            foreach (DeviceFarmUnit unit in units)
            {
                if (unit.IDDeviceFarmUnit is not int unitId) { continue; }
                string? farmName = unit.DeviceFarmID is int farmId ? farms.FirstOrDefault(f => f.IDDeviceFarm == farmId)?.DeviceFarmName : null;
                string groupLabel = farmName is null ? unit.DeviceFarmUnitName ?? "" : $"{farmName} / {unit.DeviceFarmUnitName}";
                foreach (DeviceFarmUnitZone zone in await api.DeviceFarmUnitZonesGet(unitId))
                {
                    zones.Add(new ZoneOption { IDDeviceFarmUnitZone = zone.IDDeviceFarmUnitZone!.Value, ZoneName = zone.DeviceFarmUnitZoneName ?? "", GroupLabel = groupLabel });
                }
            }
            return View(new ExperimentIndexViewModel
            {
                Experiments = await api.ExperimentList(),
                Farms = farms,
                Units = units,
                Zones = zones,
            });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string name, HierarchyNodeKind scope, int scopeId, DateTimeOffset? expiresAtUtc)
        {
            try
            {
                Experiment created = await api.ExperimentCreate(new ExperimentCreateRequest { Name = name, Scope = scope, ScopeID = scopeId, ExpiresAtUtc = expiresAtUtc });
                return RedirectToAction(nameof(Details), new { idExperiment = created.IDExperiment });
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize(Roles = RoleNames.DeviceManagersOrGlobalReader)]
        public async Task<ActionResult> Details(int idExperiment)
        {
            Experiment experiment = await api.ExperimentGet(idExperiment);
            ViewBag.Rules = await api.ExperimentRulesGet(idExperiment);
            ViewBag.SensorSamples = await api.ExperimentSensorDataGet(idExperiment);
            ViewBag.ControllerEvents = await api.ExperimentControllerDataGet(idExperiment);
            IList<DeviceFarm> farms = await api.DeviceFarmsGet();
            ViewBag.Farms = farms;
            IList<DeviceFarmUnit> units = await api.DeviceFarmUnitsGet();
            ViewBag.Units = units;
            var zones = new List<DeviceFarmUnitZone>();
            foreach (DeviceFarmUnit unit in units)
            {
                zones.AddRange(await api.DeviceFarmUnitZonesGet(unit.IDDeviceFarmUnit));
            }
            ViewBag.Zones = zones;
            return View(experiment);
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Stop(int idExperiment)
        {
            await api.ExperimentStop(idExperiment);
            return RedirectToAction(nameof(Details), new { idExperiment });
        }

        // ---- Experiment-scoped rules - same RuleFormInput/rule-builder.js as every other scope's rule form. ----

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExperimentRuleAdd(int idExperiment, RuleFormInput input)
        {
            ConditionNode? root = string.IsNullOrWhiteSpace(input.RootConditionJson)
                ? null
                : System.Text.Json.JsonSerializer.Deserialize<ConditionNode>(input.RootConditionJson, ConditionConfigJson.Options);
            var rule = new DeviceFarmUnitZoneRule
            {
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
            try
            {
                await api.ExperimentRuleAdd(idExperiment, rule);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idExperiment });
        }

        [Authorize(Roles = RoleNames.DeviceManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ExperimentRuleDelete(int idDeviceFarmUnitZoneRule, int idExperiment)
        {
            try
            {
                await api.ExperimentRuleDelete(idExperiment, idDeviceFarmUnitZoneRule);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idExperiment });
        }
    }
}
