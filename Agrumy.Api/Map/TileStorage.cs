using Agrumy.Shared;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Map
{
    /// Disk cache for proxied OSM basemap tiles - structured z/x/y like SatelliteStorage, kept forever (unlike the satellite PNG cache's 30-day retention) since a base map tile essentially never changes; nothing purges it automatically.
    public sealed class TileStorage(IOptions<AgrumySettings> settings, IHostEnvironment environment)
    {
        public const string DefaultRelativePath = "tile-store";

        public string RootPath
        {
            get
            {
                string configured = string.IsNullOrWhiteSpace(settings.Value.TileLocalPath) ? DefaultRelativePath : settings.Value.TileLocalPath;
                return Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
            }
        }

        /// Caller (TileProxy) validates z/x/y are in-range before this is ever reached - defensively re-checked anyway since a disk path is being built from them.
        public string PathFor(int z, int x, int y)
        {
            if (z < 0 || x < 0 || y < 0)
            {
                throw new ArgumentException("Tile coordinates must be non-negative.");
            }
            return Path.Combine(RootPath, z.ToString(), x.ToString(), $"{y}.png");
        }

        public byte[]? TryRead(int z, int x, int y)
        {
            string path = PathFor(z, x, y);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public async Task SaveAsync(int z, int x, int y, byte[] pngBytes, CancellationToken ct = default)
        {
            string path = PathFor(z, x, y);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Temp-then-rename, same as FirmwareStorage/SatelliteStorage - a reader mid-write never sees a truncated file.
            string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            await File.WriteAllBytesAsync(tempPath, pngBytes, ct);
            File.Move(tempPath, path, overwrite: true);
        }
    }
}
