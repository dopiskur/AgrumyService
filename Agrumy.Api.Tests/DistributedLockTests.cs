using Agrumy.Api.BackgroundWorkers;
using Agrumy.Api.Dal;
using Agrumy.Api.Dal.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Agrumy.Api.Tests;

/// Covers the PeriodicBackgroundService <-> IDistributedLock contract - a denied lock must skip DoWorkAsync entirely, not run it anyway.
public class DistributedLockTests
{
    [Fact]
    public async Task NoOpDistributedLock_AlwaysGrants_EvenReentrantly()
    {
        var sut = new NoOpDistributedLock();

        IAsyncDisposable? first = await sut.TryAcquireAsync("worker", TimeSpan.FromMinutes(1), CancellationToken.None);
        IAsyncDisposable? second = await sut.TryAcquireAsync("worker", TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(second);
        await first!.DisposeAsync();
        await second!.DisposeAsync();
    }

    private sealed class FakeLock(bool grant) : IDistributedLock
    {
        public int AcquireAttempts;

        public Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan leaseDuration, CancellationToken ct)
        {
            Interlocked.Increment(ref AcquireAttempts);
            return Task.FromResult<IAsyncDisposable?>(grant ? new Handle() : null);
        }

        private sealed class Handle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class TestWorker(IServiceScopeFactory scopeFactory)
        : PeriodicBackgroundService(scopeFactory, NullLogger<TestWorker>.Instance)
    {
        public int TicksExecuted;
        protected override TimeSpan Interval => TimeSpan.FromMilliseconds(20);

        protected override Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct)
        {
            Interlocked.Increment(ref TicksExecuted);
            return Task.CompletedTask;
        }
    }

    private static async Task<TestWorker> RunWorkerAsync(IDistributedLock distributedLock)
    {
        var services = new ServiceCollection();
        services.AddSingleton(distributedLock);
        var provider = services.BuildServiceProvider();
        var worker = new TestWorker(provider.GetRequiredService<IServiceScopeFactory>());

        var hosted = (IHostedService)worker;
        await hosted.StartAsync(CancellationToken.None);
        await Task.Delay(150);
        await hosted.StopAsync(CancellationToken.None);
        return worker;
    }

    [Fact]
    public async Task Tick_SkipsDoWork_WhenLockNotAcquired()
    {
        var fakeLock = new FakeLock(grant: false);

        TestWorker worker = await RunWorkerAsync(fakeLock);

        Assert.Equal(0, worker.TicksExecuted);
        Assert.True(fakeLock.AcquireAttempts > 0);
    }

    [Fact]
    public async Task Tick_RunsDoWork_WhenLockAcquired()
    {
        var fakeLock = new FakeLock(grant: true);

        TestWorker worker = await RunWorkerAsync(fakeLock);

        Assert.True(worker.TicksExecuted > 0);
    }
}
