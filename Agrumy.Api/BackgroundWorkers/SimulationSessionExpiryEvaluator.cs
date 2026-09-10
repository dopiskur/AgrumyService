using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Roadmap #403's hard 48h safety cutoff - a session past ExpiresAtUtc that was never explicitly stopped gets stopped here instead, turning off every member physical device's sensor override (a virtual device just stops being in an active session, so VirtualDeviceRunnerBackgroundService's own session-gated query naturally drops it next tick).
    public sealed class SimulationSessionExpiryEvaluator(ISimulationRepository simulationRepo, IDeviceRepository deviceRepo)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (SimulationSession session in await simulationRepo.SimulationSessionsExpiredButActiveGetAsync(now))
            {
                IList<int> virtualIds = await simulationRepo.VirtualDeviceIdsGetAsync(session.TenantID);
                foreach (DeviceDto member in session.Devices)
                {
                    if (member.IDDevice is int deviceId && !virtualIds.Contains(deviceId))
                    {
                        await deviceRepo.DeviceSimulationSetAsync(deviceId, new DeviceSimulation { Enabled = false });
                    }
                }
                await simulationRepo.SimulationSessionStopAsync(session.IDSimulationSession!.Value);
            }
        }
    }
}
