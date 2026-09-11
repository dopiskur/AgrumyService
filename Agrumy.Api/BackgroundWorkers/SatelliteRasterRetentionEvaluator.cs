using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Storage;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Deletes cached PNGs past retention (Detaljni dizajn S, D11) - never touches the DB grid/stats, so anything reaped here just re-renders from GridBase64 on next view. Runs per-tenant since RasterRetentionDaysOverride can differ per tenant.
    public sealed class SatelliteRasterRetentionEvaluator(ISatelliteConfigRepository configRepo, ISatelliteSceneRepository sceneRepo, IServerConfigRepository serverConfigRepo, SatelliteStorage storage, ILogger<SatelliteRasterRetentionEvaluator> logger)
    {
        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            var serverConfig = await serverConfigRepo.ServerConfigGetAsync(1);
            int defaultDays = serverConfig.SatelliteRasterRetentionDays ?? 30;

            IList<Shared.Models.TenantSatelliteConfig> configs = await configRepo.SatelliteConfigsGetEnabledAsync();
            foreach (var config in configs)
            {
                ct.ThrowIfCancellationRequested();
                int days = config.RasterRetentionDaysOverride ?? defaultDays;
                if (days <= 0)
                {
                    continue; // 0/negative disables the retention cutoff for this tenant, same "0 = disabled" convention as SensorDataRetentionDays
                }
                DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddDays(-days);
                try
                {
                    // Clears the DB's ImagePath pointer; actual file deletion under the tenant's own satellite-store subtree is a directory sweep, safe to run unconditionally since paths are entirely server-built (tenant/zone/date/index), never user text.
                    int cleared = await sceneRepo.ImagePathsClearOlderThanAsync(cutoff);
                    if (cleared > 0 && logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("Satellite raster retention: cleared {Count} ImagePath pointers for tenant {TenantId} older than {Cutoff}.", cleared, config.IDTenant, cutoff);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Satellite raster retention failed for tenant {TenantId}.", config.IDTenant);
                }
            }

            SweepStaleDirectories(storage.RootPath, DateTimeOffset.UtcNow.AddDays(-defaultDays));
        }

        /// Best-effort physical cleanup of date subdirectories older than the cutoff - the DB's ImagePathsClearOlderThanAsync above is the source of truth for "should this be regenerated"; this just reclaims disk space for files that pointer-clear already orphaned.
        private static void SweepStaleDirectories(string root, DateTimeOffset cutoff)
        {
            if (!Directory.Exists(root))
            {
                return;
            }
            foreach (string dateDir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (DateOnly.TryParseExact(Path.GetFileName(dateDir), "yyyy-MM-dd", out DateOnly parsed) &&
                    parsed.ToDateTime(TimeOnly.MinValue) < cutoff.UtcDateTime)
                {
                    try
                    {
                        Directory.Delete(dateDir, recursive: true);
                    }
                    catch (IOException)
                    {
                        // Another process may be writing a fresh render into this exact directory right now - leave it for the next daily sweep rather than risk deleting an in-progress write.
                    }
                }
            }
        }
    }
}
