namespace Agrumy.Api.Dal
{
    /// Central catalog of ICache key formats paired with the TTL each is written with, so a write site and an invalidation site can never drift onto two different formats for what's meant to be the same entry.
    public static class CacheKeys
    {
        /// Short absolute-TTL so any number of concurrently open admin tabs share one real fleet query per window instead of each re-running the full per-device scan.
        public static readonly TimeSpan FleetTtl = TimeSpan.FromSeconds(6);
        public static string Fleet(int? tenantId) => tenantId is null ? "fleet:global" : $"fleet:{tenantId}";

        // A deliberate trade-off: a revoked token can still pass for up to this long, in exchange for cutting a DB query on every authenticated request.
        public static readonly TimeSpan TokenRevocationTtl = TimeSpan.FromSeconds(30);
        public static string TokenRevocation(string email) => $"tokenRevocation:{email}";

        public static readonly TimeSpan RelayRateWindow = TimeSpan.FromMinutes(1);
        public static string RelayRate(int deviceId) => $"relay-rate:{deviceId}";

        // Read on essentially every request (device poll, every controller's organization/quota checks, every background evaluator); explicitly invalidated by every EfServerConfigRepository write path, so the TTL only bounds staleness if one of those is ever missed, not the normal case.
        public static readonly TimeSpan ServerConfigTtl = TimeSpan.FromMinutes(5);
        public static string ServerConfig(int idServerConfig) => $"serverConfig:{idServerConfig}";
    }
}
