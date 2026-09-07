using api.Dal.Interface;
using api.Models;
using api.Security;
using api.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers.View
{
    /// Roadmap #403 - "Add Simulation" is the entry point (create a named, time-boxed session first), devices are added INTO it afterward; replaces the old bare virtual-device list and the per-device Fleet toggle both.
    [Authorize(Roles = RoleNames.SimulationManagers)]
    public class SimulationController(IApi api) : Controller
    {
        public async Task<ActionResult> Index() => View(await api.SimulationSessionList());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string name, int durationMinutes)
        {
            try
            {
                SimulationSession created = await api.SimulationSessionCreate(new SimulationSessionCreateRequest { Name = name, DurationMinutes = durationMinutes });
                return RedirectToAction(nameof(Details), new { idSimulationSession = created.IDSimulationSession });
            }
            catch (ApiException ex)
            {
                TempData["Error"] = ex.Body;
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<ActionResult> Details(int idSimulationSession)
        {
            SimulationSession session = await api.SimulationSessionGet(idSimulationSession);
            ViewBag.Fleet = await api.DeviceFleetGet();
            return View(session);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Stop(int idSimulationSession)
        {
            await api.SimulationSessionStop(idSimulationSession);
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RemoveDevice(int idSimulationSession, int idDevice)
        {
            await api.SimulationSessionDeviceRemove(idSimulationSession, idDevice);
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }

        /// Creates a fully virtual device (same POST /api/Simulation/Device flow as before) and immediately adds it to this session in one step - a virtual device has no other reason to exist outside a session.
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteVirtualDevice(int idDevice, int idSimulationSession)
        {
            await api.SimulationDeviceDelete(idDevice);
            return RedirectToAction(nameof(Details), new { idSimulationSession });
        }
    }
}
