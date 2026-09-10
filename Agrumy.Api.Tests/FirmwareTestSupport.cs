using System.Text;
using Agrumy.Shared;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Firmware;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Agrumy.Api.Tests;

/// Canned network for FirmwareCatalogService tests - URL -> text (GitHub API JSON, manifests) and URL -> bytes (.bin assets); an unknown URL throws like a real 404 would.
internal sealed class FakeFirmwareFetcher : IFirmwareFetcher
{
    public Dictionary<string, string> Texts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, byte[]> Binaries { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Requested { get; } = [];

    public Task<string> GetStringAsync(string url, bool gitHubApi, CancellationToken cancellationToken = default)
    {
        Requested.Add(url);
        return Texts.TryGetValue(url, out var text)
            ? Task.FromResult(text)
            : throw new HttpRequestException("404 " + url);
    }

    public Task<Stream> GetStreamAsync(string url, CancellationToken cancellationToken = default)
    {
        Requested.Add(url);
        return Binaries.TryGetValue(url, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new HttpRequestException("404 " + url);
    }

    public static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
}

internal static class FirmwareTestSupport
{
    /// A FirmwareStorage rooted in a fresh temp directory (absolute path, so the IHostEnvironment content root is never consulted).
    public static FirmwareStorage NewStorage(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "agrumy-fw-tests", Guid.NewGuid().ToString("N"));
        var settings = Options.Create(new AgrumySettings { FirmwareLocalPath = root });
        return new FirmwareStorage(settings, new Mock<IHostEnvironment>().Object);
    }

    public static FirmwareCatalogService NewCatalog(IFirmwareRepository firmwareRepo, IServerConfigRepository configRepo, IDeviceRepository deviceRepo, IFirmwareFetcher? fetcher = null, FirmwareStorage? storage = null) =>
        new(firmwareRepo, configRepo, deviceRepo, fetcher ?? new FakeFirmwareFetcher(), storage ?? NewStorage(out _), NullLogger<FirmwareCatalogService>.Instance);

    /// The same mock backs every facet the service takes - callers add setups for IServerConfigRepository/IDeviceRepository members via repo.As&lt;T&gt;(). Both As&lt;T&gt;() registrations must happen before repo.Object is read even once (Moq freezes the mock's interface list on the first .Object access, so reading .Object off the first As&lt;T&gt;() result before registering the second one would lock the second registration out).
    public static FirmwareCatalogService NewCatalog(Mock<IFirmwareRepository> repo, IFirmwareFetcher? fetcher = null, FirmwareStorage? storage = null)
    {
        repo.As<IServerConfigRepository>();
        repo.As<IDeviceRepository>();
        return NewCatalog(repo.Object, (IServerConfigRepository)repo.Object, (IDeviceRepository)repo.Object, fetcher, storage);
    }
}
