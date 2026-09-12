using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Storage;

namespace Agrumy.Api.Arkod
{
    /// One RunOnceAsync call reports back which of these happened - lets a manual "Sync now" trigger show something more useful than a blind "done".
    public enum ArkodGeoPackageSyncOutcome
    {
        Disabled,
        UpToDate,
        Downloaded,
    }

    /// Secondary/offline path: mirrors APPRRR's whole-Croatia ARKOD GeoPackage (weekly-refreshed, ~860 MB, confirmed stable direct URL + no registration needed) so ArkodApiController.Lookup keeps working even with no outbound internet at request time. HEAD-then-conditional-GET, never blind - a daily tick only re-downloads when the upstream file actually changed.
    public sealed partial class ArkodGeoPackageSyncEvaluator(HttpClient httpClient, ArkodGeoPackageStorage storage, IServerConfigRepository serverConfigRepo, ILogger<ArkodGeoPackageSyncEvaluator> logger)
    {
        public const string SourceUrl = "https://www.apprrr.hr/wp-content/uploads/nipp/land_parcels.gpkg";

        [LoggerMessage(Level = LogLevel.Information, Message = "ARKOD GeoPackage changed upstream (or no local copy yet) - downloading.")]
        private static partial void LogDownloading(ILogger logger);

        public async Task<ArkodGeoPackageSyncOutcome> RunOnceAsync(CancellationToken ct = default)
        {
            var config = await serverConfigRepo.ServerConfigGetAsync(1);
            if (!config.ArkodGeoPackageSyncEnabled)
            {
                return ArkodGeoPackageSyncOutcome.Disabled;
            }

            using var headRequest = new HttpRequestMessage(HttpMethod.Head, SourceUrl);
            using HttpResponseMessage headResponse = await httpClient.SendAsync(headRequest, ct);
            headResponse.EnsureSuccessStatusCode();
            DateTimeOffset? upstreamLastModified = headResponse.Content.Headers.LastModified;

            bool needsDownload = !storage.Exists
                || upstreamLastModified is null // no Last-Modified header at all - can't tell, so refresh rather than risk going stale forever
                || upstreamLastModified > File.GetLastWriteTimeUtc(storage.GeoPackagePath);

            if (needsDownload)
            {
                LogDownloading(logger);
                await using Stream responseStream = await httpClient.GetStreamAsync(SourceUrl, ct);
                await storage.SaveFromStreamAsync(responseStream, ct);
                if (upstreamLastModified is DateTimeOffset lastModified)
                {
                    File.SetLastWriteTimeUtc(storage.GeoPackagePath, lastModified.UtcDateTime);
                }
            }

            await serverConfigRepo.ServerConfigArkodSyncStateSetAsync(DateTimeOffset.UtcNow, 1);
            return needsDownload ? ArkodGeoPackageSyncOutcome.Downloaded : ArkodGeoPackageSyncOutcome.UpToDate;
        }
    }
}
