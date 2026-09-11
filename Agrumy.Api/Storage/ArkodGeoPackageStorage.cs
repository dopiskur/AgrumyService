using Agrumy.Shared;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Storage
{
    /// Local mirror of APPRRR's whole-Croatia ARKOD parcel GeoPackage - one file, written either by ArkodGeoPackageSyncService's weekly download or the Server Settings manual-upload fallback, read by ArkodGeoPackageLookup. Same temp-then-rename convention as SatelliteStorage so a reader never sees a partially-written file.
    public sealed class ArkodGeoPackageStorage(IOptions<AgrumySettings> settings, IHostEnvironment environment)
    {
        public const string DefaultRelativePath = "arkod-store";
        private const string FileName = "land_parcels.gpkg";

        public string RootPath
        {
            get
            {
                string configured = string.IsNullOrWhiteSpace(settings.Value.ArkodLocalPath) ? DefaultRelativePath : settings.Value.ArkodLocalPath;
                return Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
            }
        }

        public string GeoPackagePath => Path.Combine(RootPath, FileName);

        public bool Exists => File.Exists(GeoPackagePath);

        /// Streams source into a temp file in the same directory (so the final File.Move is same-volume and atomic) then swaps it in - used by both the sync job's HTTP download and the manual-upload endpoint.
        public async Task SaveFromStreamAsync(Stream source, CancellationToken ct = default)
        {
            Directory.CreateDirectory(RootPath);
            string tempPath = GeoPackagePath + ".tmp-" + Guid.NewGuid().ToString("N");
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true))
            {
                await source.CopyToAsync(fileStream, ct);
            }
            File.Move(tempPath, GeoPackagePath, overwrite: true);
        }
    }
}
