using Agrumy.Web.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Utils;
using Agrumy.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Roadmap #403 - "Add Simulation" is the entry point (create a named, time-boxed session first), devices are added INTO it afterward; replaces the old bare virtual-device list and the per-device Fleet toggle both.
    [Authorize]
    public class SimulationController(IApi api) : Controller
    {
        [Authorize(Roles = RoleNames.SimulationManagersOrGlobalReader)]
        public async Task<ActionResult> Index() => View(await api.SimulationSessionList());

        /// Name only now, no duration; Details is where the session actually gets started.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string name)
        {
            try
            {
                SimulationSession created = await api.SimulationSessionCreate(new SimulationSessionCreateRequest { Name = name });
                return RedirectToAction(nameof(Details), new { idSimulationSession = created.IDSimulationSession });
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(int idSimulationSession)
        {
            try
            {
                await api.SimulationSessionDelete(idSimulationSession);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = RoleNames.SimulationManagersOrGlobalReader)]
        public async Task<ActionResult> Details(int idSimulationSession)
        {
            SimulationSession session = await api.SimulationSessionGet(idSimulationSession);
            ViewBag.Fleet = await api.DeviceFleetGet();
            ViewBag.Rules = await api.SessionRulesGet(idSimulationSession);
            ViewBag.Groups = await api.SessionGroupsGet(idSimulationSession);
            IList<DeviceFarmUnit> units = await api.DeviceFarmUnitsGet();
            ViewBag.Units = units;
            var zones = new List<DeviceFarmUnitZone>();
            foreach (DeviceFarmUnit unit in units)
            {
                zones.AddRange(await api.DeviceFarmUnitZonesGet(unit.IDDeviceFarmUnit));
            }
            ViewBag.Zones = zones;
            IList<FarmOpenfieldCrop> crops = await api.CropsGet();
            ViewBag.Crops = crops;
            var parcels = new List<FarmOpenfieldCropParcel>();
            foreach (FarmOpenfieldCrop crop in crops)
            {
                parcels.AddRange(await api.ParcelsGet(crop.IDFarmOpenfieldCrop));
            }
            ViewBag.Parcels = parcels;
            return View(session);
        }

        /// Starts a never-started session, or resumes one that was Stopped/expired; same action either way.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Start(int idSimulationSession, int durationMinutes)
        {
            try
            {
                await api.SimulationSessionStart(idSimulationSession, new SimulationSessionStartRequest { DurationMinutes = durationMinutes });
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Stop(int idSimulationSession)
        {
            await api.SimulationSessionStop(idSimulationSession);
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddDevice(int idSimulationSession, int idDevice)
        {
            try
            {
                await api.SimulationSessionDeviceAdd(idSimulationSession, idDevice);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveDevice(int idSimulationSession, int idDevice)
        {
            await api.SimulationSessionDeviceRemove(idSimulationSession, idDevice);
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        /// Creates a fully virtual device (same POST /api/Simulation/Device flow as before) and immediately adds it to this session in one step - a virtual device has no other reason to exist outside a session.
        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateVirtualDevice(int idSimulationSession)
        {
            try
            {
                DeviceDto created = await api.SimulationDeviceCreate();
                await api.SimulationSessionDeviceAdd(idSimulationSession, created.IDDevice!.Value);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteVirtualDevice(int idDevice, int idSimulationSession)
        {
            await api.SimulationDeviceDelete(idDevice);
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        // ---- Simulation-scoped rules - a member device evaluates these ahead of its real Zone>Unit>Farm>Global rules, same RuleFormInput/rule-builder.js as DeviceFarmUnitController's own Zone/Unit/Farm/Global rule forms. ----

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SessionRuleAdd(int idSimulationSession, RuleFormInput input)
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
                await api.SessionRuleAdd(idSimulationSession, rule);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SessionRuleDelete(int idDeviceFarmUnitZoneRule, int idSimulationSession)
        {
            try
            {
                await api.SessionRuleDelete(idSimulationSession, idDeviceFarmUnitZoneRule);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        // ---- Simulation groups - a whole Unit/Zone added at once, one override value set fanned out to every member device. ----

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SessionGroupAdd(int idSimulationSession, SimulationGroup group)
        {
            try
            {
                SimulationGroup created = await api.SessionGroupAdd(idSimulationSession, group);
                TempData["Info"] = $"Added {created.Scope} \"{created.ScopeName}\" - {created.MemberDeviceCount} device(s) now simulating.";
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SessionGroupUpdate(int idSimulationSession, int idGroup, SimulationGroup group)
        {
            try
            {
                await api.SessionGroupUpdate(idSimulationSession, idGroup, group);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        [Authorize(Roles = RoleNames.SimulationManagers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SessionGroupDelete(int idSimulationSession, int idGroup)
        {
            try
            {
                await api.SessionGroupDelete(idSimulationSession, idGroup);
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
            }
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }
    }
}
