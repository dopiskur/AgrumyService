using System.Net;
using Agrumy.Api.Dal.Interface;
using Agrumy.Api.Satellite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Agrumy.Api.Tests;

/// A minimal real (not mocked) in-memory ICache - CdseTokenProviderTests needs GetAsync to actually
/// return what SetAsync stored, which the codebase's own NullCache test double deliberately never does.
file sealed class InMemoryCache : ICache
{
    private readonly Dictionary<string, object> store = [];

    public Task<Agrumy.Shared.Models.DeviceCache> GetDeviceCacheAsync(string key) => Task.FromResult(new Agrumy.Shared.Models.DeviceCache { apiAuth = null });
    public Task SetItemAsync(string key, Agrumy.Shared.Models.DeviceCache deviceCache, TimeSpan? ttl = null) => Task.CompletedTask;
    public Task<T?> GetAsync<T>(string key) where T : class => Task.FromResult(store.TryGetValue(key, out object? v) ? (T?)v : null);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class { store[key] = value; return Task.CompletedTask; }
    public Task RemoveAsync(string key) { store.Remove(key); return Task.CompletedTask; }
}

file sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(respond(request));
    }
}

public class CdseTokenProviderTests
{
    private static HttpResponseMessage TokenResponse(string accessToken, int expiresIn = 3600) =>
        new(HttpStatusCode.OK) { Content = new StringContent($$"""{"access_token":"{{accessToken}}","expires_in":{{expiresIn}}}""", System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetAccessTokenAsync_SecondCallWithinTtl_UsesTheCacheNotANewHttpRequest()
    {
        var handler = new CountingHandler(_ => TokenResponse("token-1"));
        var httpClient = new HttpClient(handler);
        var configRepo = new Mock<ISatelliteConfigRepository>(MockBehavior.Strict);
        configRepo.Setup(r => r.SatelliteConfigCredentialsGetAsync(1)).ReturnsAsync(("client-1", "secret-1"));
        configRepo.Setup(r => r.SatelliteConfigTokenIssuedAsync(1, It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        var provider = new CdseTokenProvider(httpClient, configRepo.Object, new InMemoryCache(), NullLogger<CdseTokenProvider>.Instance);

        string? first = await provider.GetAccessTokenAsync(1, default);
        string? second = await provider.GetAccessTokenAsync(1, default);

        Assert.Equal("token-1", first);
        Assert.Equal("token-1", second);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_TwoTenants_GetIndependentTokens_NeverSharingTheCacheEntry()
    {
        var handler = new CountingHandler(request =>
        {
            string body = request.Content!.ReadAsStringAsync().Result;
            string token = body.Contains("client-a") ? "token-a" : "token-b";
            return TokenResponse(token);
        });
        var httpClient = new HttpClient(handler);
        var configRepo = new Mock<ISatelliteConfigRepository>(MockBehavior.Strict);
        configRepo.Setup(r => r.SatelliteConfigCredentialsGetAsync(1)).ReturnsAsync(("client-a", "secret-a"));
        configRepo.Setup(r => r.SatelliteConfigCredentialsGetAsync(2)).ReturnsAsync(("client-b", "secret-b"));
        configRepo.Setup(r => r.SatelliteConfigTokenIssuedAsync(It.IsAny<int>(), It.IsAny<DateTimeOffset>())).Returns(Task.CompletedTask);
        var cache = new InMemoryCache();
        var provider = new CdseTokenProvider(httpClient, configRepo.Object, cache, NullLogger<CdseTokenProvider>.Instance);

        string? tokenForTenant1 = await provider.GetAccessTokenAsync(1, default);
        string? tokenForTenant2 = await provider.GetAccessTokenAsync(2, default);

        Assert.Equal("token-a", tokenForTenant1);
        Assert.Equal("token-b", tokenForTenant2);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_NoCredentials_ReturnsNullWithoutCallingHttp()
    {
        var handler = new CountingHandler(_ => TokenResponse("should-not-be-requested"));
        var httpClient = new HttpClient(handler);
        var configRepo = new Mock<ISatelliteConfigRepository>(MockBehavior.Strict);
        configRepo.Setup(r => r.SatelliteConfigCredentialsGetAsync(1)).ReturnsAsync(((string?)null, (string?)null));
        var provider = new CdseTokenProvider(httpClient, configRepo.Object, new InMemoryCache(), NullLogger<CdseTokenProvider>.Instance);

        string? token = await provider.GetAccessTokenAsync(1, default);

        Assert.Null(token);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetAccessTokenAsync_TokenEndpointReturnsError_ReturnsNullInsteadOfThrowing()
    {
        var handler = new CountingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("invalid_client") });
        var httpClient = new HttpClient(handler);
        var configRepo = new Mock<ISatelliteConfigRepository>(MockBehavior.Strict);
        configRepo.Setup(r => r.SatelliteConfigCredentialsGetAsync(1)).ReturnsAsync(("bad-client", "bad-secret"));
        var provider = new CdseTokenProvider(httpClient, configRepo.Object, new InMemoryCache(), NullLogger<CdseTokenProvider>.Instance);

        string? token = await provider.GetAccessTokenAsync(1, default);

        Assert.Null(token);
    }

    [Fact]
    public async Task TryGetAccessTokenForCredentialsAsync_NeverTouchesTheConfigRepositoryOrCache()
    {
        var handler = new CountingHandler(_ => TokenResponse("unsaved-token"));
        var httpClient = new HttpClient(handler);
        var provider = new CdseTokenProvider(httpClient, Mock.Of<ISatelliteConfigRepository>(MockBehavior.Strict), new InMemoryCache(), NullLogger<CdseTokenProvider>.Instance);

        (bool ok, string? error) = await provider.TryGetAccessTokenForCredentialsAsync("unsaved-client", "unsaved-secret", default);

        Assert.True(ok);
        Assert.Null(error);
    }
}
