using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Map;
using Agrumy.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Agrumy.Api.Tests;

/// Exercises TileProxy's z/x/y bounds validation and cache-hit short-circuit directly, no real network call - a temp-directory-rooted TileStorage (same pattern as FirmwareTestSupport.NewStorage) stands in for the disk cache, and a Strict IHttpClientFactory mock doubles as the assertion that a cache hit never reaches the network.
public class TileProxyTests
{
    private static TileStorage NewStorage(out string root)
    {
        root = Path.Combine(Path.GetTempPath(), "agrumy-tile-tests", Guid.NewGuid().ToString("N"));
        var settings = Options.Create(new AgrumySettings { TileLocalPath = root });
        return new TileStorage(settings, new Mock<IHostEnvironment>().Object);
    }

    private static TileProxy NewController(TileStorage storage, Mock<IHttpClientFactory>? httpClientFactory = null)
    {
        var userRepo = new Mock<IUserRepository>(MockBehavior.Strict);
        var auditLogRepo = new Mock<IAuditLogRepository>(MockBehavior.Strict);
        var cache = new Mock<ICache>(MockBehavior.Strict);
        return new TileProxy(storage, new OsmTileRateLimiter(), (httpClientFactory ?? new Mock<IHttpClientFactory>(MockBehavior.Strict)).Object,
            userRepo.Object, auditLogRepo.Object, cache.Object, NullLogger<TileProxy>.Instance);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(20)]
    public async Task Get_ZOutOfRange_ReturnsBadRequest(int z)
    {
        var controller = NewController(NewStorage(out _));

        var result = await controller.Get(z, 0, 0, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        // Strict IHttpClientFactory mock: CreateClient would throw if the network path were ever reached.
    }

    [Theory]
    [InlineData(5, -1, 0)]
    [InlineData(5, 0, -1)]
    [InlineData(5, 32, 0)] // 2^5 = 32, so x must be 0-31
    [InlineData(5, 0, 32)]
    public async Task Get_XYOutOfRangeForZ_ReturnsBadRequest(int z, int x, int y)
    {
        var controller = NewController(NewStorage(out _));

        var result = await controller.Get(z, x, y, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Get_CacheHit_ReturnsCachedBytes_NeverTouchesTheNetwork()
    {
        TileStorage storage = NewStorage(out _);
        byte[] pngBytes = { 1, 2, 3, 4 };
        await storage.SaveAsync(5, 10, 12, pngBytes);
        // Strict mock never set up to return anything from CreateClient - a cache-hit call would throw here if it fell through to the fetch path.
        var controller = NewController(storage);

        var result = await controller.Get(5, 10, 12, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(pngBytes, file.FileContents);
        Assert.Equal("image/png", file.ContentType);
    }
}
