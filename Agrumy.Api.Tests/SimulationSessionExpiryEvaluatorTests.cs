using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises SimulationSessionExpiryEvaluator directly - no database, its two facets are mocked.
public class SimulationSessionExpiryEvaluatorTests
{
    private readonly Mock<ISimulationRepository> _repo = new(MockBehavior.Strict);
    private IDeviceRepository DeviceRepo => _repo.As<IDeviceRepository>().Object;

    // DeviceRepo (Mock.As&lt;T&gt;()) must be touched before _repo.Object is ever read - Moq locks the mock's interface set on first .Object access, and constructor args evaluate left to right.
    private SimulationSessionExpiryEvaluator NewEvaluator()
    {
        IDeviceRepository deviceRepo = DeviceRepo;
        return new(_repo.Object, deviceRepo);
    }

    [Fact]
    public async Task NoExpiredSessions_DoesNothing()
    {
        _repo.Setup(r => r.SimulationSessionsExpiredButActiveGetAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync(new List<SimulationSession>());

        await NewEvaluator().RunOnceAsync();

        // Strict mock: any call beyond the worklist fetch (VirtualDeviceIdsGetAsync, DeviceSimulationSetAsync, SimulationSessionStopAsync) would throw.
    }

    [Fact]
    public async Task ExpiredSession_TurnsOffPhysicalMembers_LeavesVirtualAlone_ThenStops()
    {
        var session = new SimulationSession
        {
            IDSimulationSession = 7,
            TenantID = 1,
            Devices = [new DeviceDto { IDDevice = 8 }, new DeviceDto { IDDevice = 9 }],
        };
        _repo.Setup(r => r.SimulationSessionsExpiredButActiveGetAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync(new List<SimulationSession> { session });
        _repo.Setup(r => r.VirtualDeviceIdsGetAsync(1)).ReturnsAsync(new List<int> { 9 }); // 9 is virtual, 8 is physical
        _repo.As<IDeviceRepository>().Setup(r => r.DeviceSimulationSetAsync(8, It.Is<DeviceSimulation>(s => !s.Enabled))).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SimulationSessionStopAsync(7)).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _repo.As<IDeviceRepository>().Verify(r => r.DeviceSimulationSetAsync(8, It.IsAny<DeviceSimulation>()), Times.Once);
        // Strict mock: DeviceSimulationSetAsync(9, ...) has no setup, proving the virtual member was never touched.
    }

    [Fact]
    public async Task MultipleExpiredSessions_AreAllStopped()
    {
        var s1 = new SimulationSession { IDSimulationSession = 1, TenantID = 1, Devices = [] };
        var s2 = new SimulationSession { IDSimulationSession = 2, TenantID = 2, Devices = [] };
        _repo.Setup(r => r.SimulationSessionsExpiredButActiveGetAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync(new List<SimulationSession> { s1, s2 });
        _repo.Setup(r => r.VirtualDeviceIdsGetAsync(1)).ReturnsAsync(new List<int>());
        _repo.Setup(r => r.VirtualDeviceIdsGetAsync(2)).ReturnsAsync(new List<int>());
        _repo.Setup(r => r.SimulationSessionStopAsync(1)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SimulationSessionStopAsync(2)).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _repo.Verify(r => r.SimulationSessionStopAsync(1), Times.Once);
        _repo.Verify(r => r.SimulationSessionStopAsync(2), Times.Once);
        // Strict mock: DeviceSimulationSetAsync has no setup, proving an empty Devices list skips the member loop entirely.
    }
}
