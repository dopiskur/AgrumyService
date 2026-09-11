using Agrumy.Shared;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Storage
{
    /// The rendered-PNG cache directory (Detaljni dizajn S, D7/D10) - structured, not GUID-flat like FirmwareStorage/FieldLogAttachmentStorage, because every path segment (tenant/zone/date/index) is server-controlled data, never a user-supplied file name. Purely a cache: SatelliteRasterRetentionEvaluator deletes freely, the next view just re-renders from the grid already in the DB.
    public sealed class SatelliteStorage(IOptions<AgrumySettings> settings, IHostEnvironment environment)
    {
        public const string DefaultRelativePath = "satellite-store";

        public string RootPath
        {
            get
            {
                string configured = string.IsNullOrWhiteSpace(settings.Value.SatelliteLocalPath) ? DefaultRelativePath : settings.Value.SatelliteLocalPath;
                return Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
            }
        }

        public string PathFor(int tenantId, int farmParcelZoneId, DateOnly sceneDate, Agrumy.Shared.Models.SatelliteIndex index) =>
            Path.Combine(RootPath, tenantId.ToString(), farmParcelZoneId.ToString(), sceneDate.ToString("yyyy-MM-dd"), $"{index}.png");

        public async Task SaveAsync(string path, byte[] pngBytes, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Temp-then-rename, same as FirmwareStorage - a reader mid-write never sees a truncated file.
            string tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            await File.WriteAllBytesAsync(tempPath, pngBytes, ct);
            File.Move(tempPath, path, overwrite: true);
        }

        public byte[]? TryRead(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;

        public void Delete(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
