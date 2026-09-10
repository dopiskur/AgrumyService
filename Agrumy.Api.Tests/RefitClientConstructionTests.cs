using System.Net;
using Agrumy.Web.Dal.Interface;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Agrumy.Api.Tests;

/// Boots Agrumy.Web with the REAL (non-mocked) Refit-registered IApi/IAuthApi, unlike WebEndpointTests' WebWebApplicationFactory which replaces IApi with a Moq double - this is what silently broke in production before the Refit.Reflection re-add (DI only throws the first time a controller injecting IApi is actually instantiated, which no Moq-based or curl-only check ever exercises).
public sealed class RefitClientConstructionTests : IClassFixture<WebApplicationFactory<Agrumy.Web.WebHostMarker>>
{
    private readonly WebApplicationFactory<Agrumy.Web.WebHostMarker> _factory;

    public RefitClientConstructionTests(WebApplicationFactory<Agrumy.Web.WebHostMarker> factory)
    {
        // Unreachable but syntactically valid - a resolvable DI graph is what's under test here, not a live backend.
        Environment.SetEnvironmentVariable("WebView__ApiService", "http://127.0.0.1:1");
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    [Fact]
    public void RealIApiAndIAuthApi_ResolveFromDI_WithoutThrowing()
    {
        using var scope = _factory.Services.CreateScope();

        IApi api = scope.ServiceProvider.GetRequiredService<IApi>();
        IAuthApi authApi = scope.ServiceProvider.GetRequiredService<IAuthApi>();

        Assert.NotNull(api);
        Assert.NotNull(authApi);
    }

    [Fact]
    public async Task Login_ConstructsControllerWithRealIApiClient_WithoutServerError()
    {
        using HttpClient client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        HttpResponseMessage response = await client.GetAsync("/Login");

        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
