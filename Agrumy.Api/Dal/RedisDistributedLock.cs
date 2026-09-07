using Agrumy.Api.Dal.Interface;
using StackExchange.Redis;

namespace Agrumy.Api.Dal
{
    /// SET NX PX for acquisition; release runs a compare-and-delete Lua script so a lock only ever clears its own token, never one a slower holder's expired lease already handed to someone else.
    internal sealed class RedisDistributedLock(IConnectionMultiplexer redis) : IDistributedLock
    {
        private const string ReleaseScript =
            "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";

        private sealed class Handle(IDatabase db, RedisKey key, RedisValue token) : IAsyncDisposable
        {
            public async ValueTask DisposeAsync() =>
                await db.ScriptEvaluateAsync(ReleaseScript, [key], [token]);
        }

        public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan leaseDuration, CancellationToken ct)
        {
            var db = redis.GetDatabase();
            RedisKey lockKey = $"lock:{key}";
            RedisValue token = Guid.NewGuid().ToString("N");
            bool acquired = await db.StringSetAsync(lockKey, token, leaseDuration, When.NotExists);
            return acquired ? new Handle(db, lockKey, token) : null;
        }
    }
}
