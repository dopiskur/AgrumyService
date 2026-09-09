using Agrumy.Api.Dal.Interface;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Reusable recurring-work base for hosted services; each tick runs in its own DI scope since IHostedService is singleton-lifetime, and one tick throwing is logged without killing the loop.
    public abstract class PeriodicBackgroundService(IServiceScopeFactory scopeFactory, ILogger logger) : BackgroundService
    {
        protected abstract TimeSpan Interval { get; }

        private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

        /// Null until the first tick completes without throwing.
        public DateTimeOffset? LastSuccessfulTickUtc { get; private set; }

        /// Same "missed more than 2x its own interval" grace WeatherHealthCheck used before this generalized it - one slow/failed tick alone shouldn't flag a worker.
        public bool IsStale => DateTimeOffset.UtcNow - (LastSuccessfulTickUtc ?? startedAtUtc) > Interval + Interval;

        /// One tick's work, given a fresh DI scope's IServiceProvider; let it throw - ExecuteAsync isolates one tick's failure from the next.
        protected abstract Task DoWorkAsync(IServiceProvider scopedProvider, CancellationToken ct);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            // do{}while, not while{}: runs once immediately on startup rather than waiting a full Interval for the first tick.
            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var distributedLock = scope.ServiceProvider.GetRequiredService<IDistributedLock>();

                    // Lease = Interval: covers one tick's normal runtime, and self-heals if a replica dies mid-tick instead of holding the key forever.
                    await using var held = await distributedLock.TryAcquireAsync(GetType().Name, Interval, stoppingToken);
                    if (held is null)
                    {
                        continue; // another replica already owns this tick
                    }

                    await DoWorkAsync(scope.ServiceProvider, stoppingToken);
                    LastSuccessfulTickUtc = DateTimeOffset.UtcNow;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // normal shutdown, not a tick failure
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Worker} tick failed - will retry next interval.", GetType().Name);
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
