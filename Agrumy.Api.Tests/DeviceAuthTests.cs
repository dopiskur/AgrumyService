using System.Security.Claims;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Agrumy.Api.Tests;

/// Drives DeviceApiKeyHandler/DeviceSessionHandler through every rejection path (missing header/unknown device/bad key/expired-session) plus the success path, end to end through the real LoggerMessage delegates.
public class DeviceAuthTests
{
    private static AuthorizationHandlerContext NewContext(IAuthorizationRequirement requirement, HttpContext http) =>
        new([requirement], new ClaimsPrincipal(new ClaimsIdentity()), http);

    private static DefaultHttpContext HttpWithHeaders(string? apiId = null, string? apiKey = null, string? authToken = null)
    {
        var http = new DefaultHttpContext();
        if (apiId != null) { http.Request.Headers["apiId"] = apiId; }
        if (apiKey != null) { http.Request.Headers["apiKey"] = apiKey; }
        if (authToken != null) { http.Request.Headers.Authorization = authToken; }
        return http;
    }


    [Fact]
    public async Task ApiKey_MissingHeader_Fails()
    {
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        var handler = new DeviceApiKeyHandler(repo.Object, NullLogger<DeviceApiKeyHandler>.Instance);
        var context = NewContext(new DeviceApiKeyRequirement(), HttpWithHeaders());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        repo.Verify(r => r.DeviceGetByApiIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ApiKey_UnknownDevice_Fails()
    {
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        repo.Setup(r => r.DeviceGetByApiIdAsync("ghost")).ReturnsAsync((Device?)null);
        var handler = new DeviceApiKeyHandler(repo.Object, NullLogger<DeviceApiKeyHandler>.Instance);
        var context = NewContext(new DeviceApiKeyRequirement(), HttpWithHeaders(apiId: "ghost", apiKey: "whatever"));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ApiKey_WrongKey_Fails()
    {
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        repo.Setup(r => r.DeviceGetByApiIdAsync("dev1")).ReturnsAsync(new Device { ApiId = "dev1", ApiKey = "correct" });
        var handler = new DeviceApiKeyHandler(repo.Object, NullLogger<DeviceApiKeyHandler>.Instance);
        var context = NewContext(new DeviceApiKeyRequirement(), HttpWithHeaders(apiId: "dev1", apiKey: "wrong"));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task ApiKey_CorrectKey_Succeeds()
    {
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        repo.Setup(r => r.DeviceGetByApiIdAsync("dev1")).ReturnsAsync(new Device { ApiId = "dev1", ApiKey = "correct" });
        var handler = new DeviceApiKeyHandler(repo.Object, NullLogger<DeviceApiKeyHandler>.Instance);
        var http = HttpWithHeaders(apiId: "dev1", apiKey: "correct");
        var context = NewContext(new DeviceApiKeyRequirement(), http);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal("dev1", http.DeviceApiId());
    }


    [Fact]
    public async Task Session_MissingHeader_Fails()
    {
        var cache = new Mock<ICache>(MockBehavior.Strict);
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        var handler = new DeviceSessionHandler(cache.Object, repo.Object, NullLogger<DeviceSessionHandler>.Instance);
        var context = NewContext(new DeviceSessionRequirement(), HttpWithHeaders());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        cache.Verify(c => c.GetDeviceCacheAsync(It.IsAny<string>()), Times.Never);
        repo.Verify(r => r.DeviceSessionGetAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Session_ValidToken_Succeeds()
    {
        var cache = new Mock<ICache>(MockBehavior.Strict);
        cache.Setup(c => c.GetDeviceCacheAsync("dev1")).ReturnsAsync(new DeviceCache { apiAuth = "sometoken" });
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        var handler = new DeviceSessionHandler(cache.Object, repo.Object, NullLogger<DeviceSessionHandler>.Instance);
        var http = HttpWithHeaders(apiId: "dev1", authToken: "Bearer sometoken");
        var context = NewContext(new DeviceSessionRequirement(), http);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal("dev1", http.DeviceApiId());
        // The fast cache path succeeded - the DB fallback must never be consulted.
        repo.Verify(r => r.DeviceSessionGetAsync(It.IsAny<string>()), Times.Never);
    }

    // Cache miss (server restart/redeploy wiped the in-process cache, or a different instance behind a load balancer) falls back to the DB-persisted token before rejecting.

    [Fact]
    public async Task Session_CacheMiss_NoDbRow_Fails()
    {
        var cache = new Mock<ICache>(MockBehavior.Strict);
        cache.Setup(c => c.GetDeviceCacheAsync("dev1")).ReturnsAsync(new DeviceCache());
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        repo.Setup(r => r.DeviceSessionGetAsync("dev1")).ReturnsAsync(((string, DateTimeOffset)?)null);
        var handler = new DeviceSessionHandler(cache.Object, repo.Object, NullLogger<DeviceSessionHandler>.Instance);
        var context = NewContext(new DeviceSessionRequirement(), HttpWithHeaders(apiId: "dev1", authToken: "Bearer sometoken"));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Session_CacheMiss_DbTokenExpired_Fails()
    {
        var cache = new Mock<ICache>(MockBehavior.Strict);
        cache.Setup(c => c.GetDeviceCacheAsync("dev1")).ReturnsAsync(new DeviceCache());
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        repo.Setup(r => r.DeviceSessionGetAsync("dev1")).ReturnsAsync(("sometoken", DateTimeOffset.UtcNow.AddMinutes(-1)));
        var handler = new DeviceSessionHandler(cache.Object, repo.Object, NullLogger<DeviceSessionHandler>.Instance);
        var context = NewContext(new DeviceSessionRequirement(), HttpWithHeaders(apiId: "dev1", authToken: "Bearer sometoken"));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Session_CacheMiss_DbTokenMismatch_Fails()
    {
        var cache = new Mock<ICache>(MockBehavior.Strict);
        cache.Setup(c => c.GetDeviceCacheAsync("dev1")).ReturnsAsync(new DeviceCache());
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        repo.Setup(r => r.DeviceSessionGetAsync("dev1")).ReturnsAsync(("differenttoken", DateTimeOffset.UtcNow.AddMinutes(30)));
        var handler = new DeviceSessionHandler(cache.Object, repo.Object, NullLogger<DeviceSessionHandler>.Instance);
        var context = NewContext(new DeviceSessionRequirement(), HttpWithHeaders(apiId: "dev1", authToken: "Bearer sometoken"));

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Session_CacheMiss_DbTokenValid_SucceedsAndRepopulatesCache()
    {
        var cache = new Mock<ICache>(MockBehavior.Strict);
        cache.Setup(c => c.GetDeviceCacheAsync("dev1")).ReturnsAsync(new DeviceCache());
        cache.Setup(c => c.SetItemAsync("dev1", It.Is<DeviceCache>(d => d.apiAuth == "sometoken"), It.IsAny<TimeSpan?>()))
            .Returns(Task.CompletedTask);
        var repo = new Mock<IDeviceRepository>(MockBehavior.Strict);
        // DeviceSessionGetAsync now returns a SHA-256 hash of the real token (DeviceSessionSetAsync's write side), not the raw value - the handler hashes the incoming header token before comparing.
        repo.Setup(r => r.DeviceSessionGetAsync("dev1")).ReturnsAsync((Agrumy.Api.Security.DeviceAuth.HashSessionToken("sometoken"), DateTimeOffset.UtcNow.AddMinutes(30)));
        var handler = new DeviceSessionHandler(cache.Object, repo.Object, NullLogger<DeviceSessionHandler>.Instance);
        var http = HttpWithHeaders(apiId: "dev1", authToken: "Bearer sometoken");
        var context = NewContext(new DeviceSessionRequirement(), http);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        Assert.Equal("dev1", http.DeviceApiId());
        cache.Verify(c => c.SetItemAsync("dev1", It.Is<DeviceCache>(d => d.apiAuth == "sometoken"), It.IsAny<TimeSpan?>()), Times.Once);
    }
}
