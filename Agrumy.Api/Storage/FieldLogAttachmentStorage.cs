using Agrumy.Shared;
using Microsoft.Extensions.Options;

namespace Agrumy.Api.Storage
{
    /// The directory dnevnik attachments (soil analysis lab reports, spray records, photos) are saved under - same "own flat store directory, GUID-named on disk" pattern as FirmwareStorage, except file names here are never trusted (user uploads), so every on-disk name is server-generated.
    public sealed class FieldLogAttachmentStorage(IOptions<AgrumySettings> settings, IHostEnvironment environment)
    {
        public const string DefaultRelativePath = "fieldlog-store";

        public string RootPath
        {
            get
            {
                string configured = string.IsNullOrWhiteSpace(settings.Value.FieldLogAttachmentLocalPath) ? DefaultRelativePath : settings.Value.FieldLogAttachmentLocalPath;
                return Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
            }
        }

        /// Saves under a fresh GUID name (the original file name is kept only in FieldLogAttachment.FileName for display/download, never used as a path) and returns that stored name plus the byte count.
        public async Task<(string StoredName, long SizeBytes)> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(RootPath);
            string storedName = Guid.NewGuid().ToString("N") + extension;
            string path = Path.Combine(RootPath, storedName);
            await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(file, cancellationToken);
            return (storedName, file.Length);
        }

        public string PathFor(string storedName) => Path.Combine(RootPath, storedName);

        public void Delete(string storedName)
        {
            string path = PathFor(storedName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
