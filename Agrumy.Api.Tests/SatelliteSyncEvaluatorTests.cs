using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Notifications;
using Agrumy.Api.Satellite;
using Agrumy.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Agrumy.Api.Tests;

public class SatelliteSyncEvaluatorTests
{
    private readonly Mock<ISatelliteConfigRepository> _configRepo = new(MockBehavior.Strict);
    private readonly Mock<IFarmParcelRepository> _farmParcelRepo = new(MockBehavior.Strict);
    private readonly Mock<ISatelliteSceneRepository> _sceneRepo = new(MockBehavior.Strict);
    private readonly Mock<ISatelliteImagerySourceFactory> _sourceFactory = new(MockBehavior.Strict);
    private readonly Mock<ISatelliteImagerySource> _source = new(MockBehavior.Strict);
    private readonly Mock<IUserRepository> _userRepo = new(MockBehavior.Strict);
    private readonly Mock<INotificationDispatcher> _dispatcher = new(MockBehavior.Strict);

    private SatelliteSyncEvaluator NewEvaluator() =>
        new(_configRepo.Object, _farmParcelRepo.Object, _sceneRepo.Object, _sourceFactory.Object, _userRepo.Object, _dispatcher.Object, NullLogger<SatelliteSyncEvaluator>.Instance);

    private static TenantSatelliteConfig NewConfig(int tenantId, DateTimeOffset? quotaPausedUntilUtc = null, DateTimeOffset? quotaPausedNotifiedAtUtc = null,
        int syncIntervalDays = 1, DateTimeOffset? lastAutoSyncUtc = null) => new()
    {
        IDTenant = tenantId,
        Provider = SatelliteProvider.CdseSentinelHub,
        Enabled = true,
        MaxCloudPercent = 40,
        MinValidPixelPercent = 70,
        DefaultIndices = [SatelliteIndex.Ndvi],
        QuotaPausedUntilUtc = quotaPausedUntilUtc,
        QuotaPausedNotifiedAtUtc = quotaPausedNotifiedAtUtc,
        SyncIntervalDays = syncIntervalDays,
        LastAutoSyncUtc = lastAutoSyncUtc,
    };

    [Fact]
    public async Task RunOnceAsync_QuotaAt90Percent_PausesAndNotifiesOnce()
    {
        TenantSatelliteConfig config = NewConfig(tenantId: 1);
        _configRepo.Setup(r => r.SatelliteConfigsGetEnabledAsync()).ReturnsAsync([config]);
        _sourceFactory.Setup(f => f.ForAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(_source.Object);
        _source.Setup(s => s.QuotaStatusAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new QuotaSnapshot(92.0, "{}"));
        _configRepo.Setup(r => r.SatelliteConfigQuotaSnapshotSetAsync(1, It.IsAny<string>(), It.IsAny<DateTimeOffset?>())).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.TenantAdminsGetAsync(1)).ReturnsAsync(new List<User> { new() { IDUser = 5, Email = "admin@example.com" } });
        _userRepo.Setup(r => r.NotificationDisabledChannelsGetAsync(It.IsAny<IEnumerable<int>>(), NotificationEventType.SatelliteQuotaPaused))
            .ReturnsAsync(new Dictionary<int, HashSet<string>>());
        _dispatcher.Setup(d => d.DispatchToRecipientsAsync(It.IsAny<string>(), It.IsAny<string>(), NotificationSeverity.Warning, It.IsAny<IReadOnlyList<NotificationRecipient>>(), false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChannelOutcome>());
        _configRepo.Setup(r => r.SatelliteConfigQuotaPausedNotifiedAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        _configRepo.Setup(r => r.SatelliteConfigLastAutoSyncSetAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _configRepo.Verify(r => r.SatelliteConfigQuotaSnapshotSetAsync(1, It.IsAny<string>(), It.Is<DateTimeOffset?>(d => d != null)), Times.Once);
        _dispatcher.Verify(d => d.DispatchToRecipientsAsync(It.Is<string>(s => s.Contains("quota")), It.IsAny<string>(), NotificationSeverity.Warning, It.IsAny<IReadOnlyList<NotificationRecipient>>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_AlreadyPausedAndNotified_DoesNotNotifyAgain()
    {
        TenantSatelliteConfig config = NewConfig(tenantId: 1, quotaPausedUntilUtc: DateTimeOffset.UtcNow.AddDays(10), quotaPausedNotifiedAtUtc: DateTimeOffset.UtcNow.AddHours(-1));
        _configRepo.Setup(r => r.SatelliteConfigsGetEnabledAsync()).ReturnsAsync([config]);
        // Still paused (QuotaPausedUntilUtc in the future) - RunForTenantAsync should return immediately, never touching the source/quota/notification path at all.
        _configRepo.Setup(r => r.SatelliteConfigLastAutoSyncSetAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _sourceFactory.VerifyNoOtherCalls();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunOnceAsync_QuotaBelowThreshold_DoesNotPauseOrNotify()
    {
        TenantSatelliteConfig config = NewConfig(tenantId: 1);
        _configRepo.Setup(r => r.SatelliteConfigsGetEnabledAsync()).ReturnsAsync([config]);
        _sourceFactory.Setup(f => f.ForAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(_source.Object);
        _source.Setup(s => s.QuotaStatusAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new QuotaSnapshot(45.0, "{}"));
        _configRepo.Setup(r => r.SatelliteConfigQuotaSnapshotSetAsync(1, It.IsAny<string>(), null)).Returns(Task.CompletedTask);
        _source.Setup(s => s.GetCapabilities(SatelliteCollection.Sentinel2)).Returns(new SatelliteCapabilities([SatelliteIndex.Ndvi], 10, 5, 300, true));
        _farmParcelRepo.Setup(r => r.FarmParcelZonesWithGeometryGetAsync(1)).ReturnsAsync(new List<FarmParcelZone>());
        _configRepo.Setup(r => r.SatelliteConfigLastAutoSyncSetAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _configRepo.Verify(r => r.SatelliteConfigQuotaSnapshotSetAsync(1, It.IsAny<string>(), null), Times.Once);
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunOnceAsync_TenantWithNoConfiguredProvider_IsSkippedWithoutError()
    {
        TenantSatelliteConfig config = NewConfig(tenantId: 1);
        _configRepo.Setup(r => r.SatelliteConfigsGetEnabledAsync()).ReturnsAsync([config]);
        _sourceFactory.Setup(f => f.ForAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((ISatelliteImagerySource?)null);
        _configRepo.Setup(r => r.SatelliteConfigLastAutoSyncSetAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _source.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunOnceAsync_OneTenantThrows_OtherTenantsStillRun()
    {
        TenantSatelliteConfig failingConfig = NewConfig(tenantId: 1);
        TenantSatelliteConfig okConfig = NewConfig(tenantId: 2);
        _configRepo.Setup(r => r.SatelliteConfigsGetEnabledAsync()).ReturnsAsync([failingConfig, okConfig]);
        _sourceFactory.Setup(f => f.ForAsync(1, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        _userRepo.Setup(r => r.TenantAdminsGetAsync(1)).ReturnsAsync(new List<User>());
        _sourceFactory.Setup(f => f.ForAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(_source.Object);
        _source.Setup(s => s.QuotaStatusAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(new QuotaSnapshot(10.0, "{}"));
        _configRepo.Setup(r => r.SatelliteConfigQuotaSnapshotSetAsync(2, It.IsAny<string>(), null)).Returns(Task.CompletedTask);
        _source.Setup(s => s.GetCapabilities(SatelliteCollection.Sentinel2)).Returns(new SatelliteCapabilities([SatelliteIndex.Ndvi], 10, 5, 300, true));
        _farmParcelRepo.Setup(r => r.FarmParcelZonesWithGeometryGetAsync(2)).ReturnsAsync(new List<FarmParcelZone>());
        _configRepo.Setup(r => r.SatelliteConfigLastAutoSyncSetAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        _configRepo.Setup(r => r.SatelliteConfigLastAutoSyncSetAsync(2, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        // Tenant 2 still ran to completion despite tenant 1's exception.
        _source.Verify(s => s.QuotaStatusAsync(2, It.IsAny<CancellationToken>()), Times.Once);
        // LastAutoSyncUtc still advances for a tenant whose tick threw - the daily budget was spent attempting it, so a broken tenant doesn't retry every single tick forever.
        _configRepo.Verify(r => r.SatelliteConfigLastAutoSyncSetAsync(1, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_IntervalNotElapsedYet_SkipsTenantEntirely()
    {
        TenantSatelliteConfig config = NewConfig(tenantId: 1, syncIntervalDays: 7, lastAutoSyncUtc: DateTimeOffset.UtcNow.AddDays(-2));
        _configRepo.Setup(r => r.SatelliteConfigsGetEnabledAsync()).ReturnsAsync([config]);

        await NewEvaluator().RunOnceAsync();

        // Strict mocks: SourceFactory/LastAutoSyncSetAsync would throw if called - a tenant not yet due gets no work at all, not even a timestamp bump.
        _sourceFactory.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunOnceAsync_IntervalElapsed_RunsAndAdvancesLastAutoSyncUtc()
    {
        TenantSatelliteConfig config = NewConfig(tenantId: 1, syncIntervalDays: 7, lastAutoSyncUtc: DateTimeOffset.UtcNow.AddDays(-8));
        _configRepo.Setup(r => r.SatelliteConfigsGetEnabledAsync()).ReturnsAsync([config]);
        _sourceFactory.Setup(f => f.ForAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((ISatelliteImagerySource?)null);
        _configRepo.Setup(r => r.SatelliteConfigLastAutoSyncSetAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);

        await NewEvaluator().RunOnceAsync();

        _configRepo.Verify(r => r.SatelliteConfigLastAutoSyncSetAsync(1, It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task RunOnceAsync_ManualTrigger_IgnoresIntervalAndNeverTouchesLastAutoSyncUtc()
    {
        // Due today (interval hasn't elapsed) - a manual "Sync now" must run anyway and must not write LastAutoSyncUtc, so it can never shift the next automatic run.
        TenantSatelliteConfig config = NewConfig(tenantId: 1, syncIntervalDays: 30, lastAutoSyncUtc: DateTimeOffset.UtcNow.AddHours(-1));
        _configRepo.Setup(r => r.SatelliteConfigsGetEnabledAsync()).ReturnsAsync([config]);
        _sourceFactory.Setup(f => f.ForAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((ISatelliteImagerySource?)null);

        await NewEvaluator().RunOnceAsync(isManualTrigger: true);

        // Strict mock: SatelliteConfigLastAutoSyncSetAsync would throw if called.
        _source.VerifyNoOtherCalls();
    }
}
