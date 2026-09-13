using System.Security.Claims;
using System.Text.Encodings.Web;
using Agrumy.Shared.Models;
using Agrumy.Shared.Security;
using Agrumy.Web.Dal.Interface;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Agrumy.Api.Tests;

/// Reads the caller's role list from a request header instead of a real login cookie - lets WebEndpointTests assert role-based rendering without driving Agrumy.Web's actual Login flow.
public sealed class TestRoleAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestRole";
    public const string RoleHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var roleHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }
        var claims = roleHeader.ToString().Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => new Claim(ClaimTypes.Role, r))
            .Append(new Claim(ClaimTypes.Name, "webtests@example.com"))
            .ToList();
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// Drives Agrumy.Web's real MVC/Razor pipeline with a mocked IApi (no real Agrumy.Api needed) and TestRoleAuthHandler standing in for cookie login - see ApiWebApplicationFactory in HttpEndpointTests.cs for the API-side equivalent.
public sealed class WebWebApplicationFactory : WebApplicationFactory<Agrumy.Web.WebHostMarker>
{
    public readonly Mock<IApi> ApiMock = new();

    public WebWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("WebView__ApiService", "http://127.0.0.1:1");

        // _Layout.cshtml calls these on every page regardless of which controller/action is under test.
        ApiMock.Setup(a => a.DeviceFarmsGet()).ReturnsAsync(new List<DeviceFarm>());
        ApiMock.Setup(a => a.EmergencyStopStatus(It.IsAny<int?>())).ReturnsAsync(false);

        ApiMock.Setup(a => a.ServerConfigGet()).ReturnsAsync(new ServerConfig());
        ApiMock.Setup(a => a.ServerConfigGetPublic()).ReturnsAsync(new PublicServerConfig());
        ApiMock.Setup(a => a.ServerConfigGetHealth()).ReturnsAsync(new List<ServerHealthEntry>());
        ApiMock.Setup(a => a.TenantWeatherStateGet(It.IsAny<int?>())).ReturnsAsync(new TenantWeatherState());
        ApiMock.Setup(a => a.WebhookSsrfAllowlistGet()).ReturnsAsync(new List<SsrfAllowlistEntry>());
        ApiMock.Setup(a => a.FirmwareSsrfAllowlistGet()).ReturnsAsync(new List<SsrfAllowlistEntry>());
        ApiMock.Setup(a => a.UsersGet()).ReturnsAsync(new List<User>
        {
            new() { IDUser = 1, Email = "member@example.com", Username = "member", Enabled = true },
        });
        ApiMock.Setup(a => a.DeviceGet(It.IsAny<int?>())).ReturnsAsync(new DeviceDto
        {
            IDDevice = 1,
            TenantID = 1,
            DeviceRoleID = 1,
            DeviceTypeServiceID = 1,
            DeviceName = "Test device",
            MacAddress = "AA:BB:CC:DD:EE:FF",
        });
        ApiMock.Setup(a => a.DeviceRoleGet()).ReturnsAsync(new List<DeviceRole>());
        ApiMock.Setup(a => a.DeviceTypeGet()).ReturnsAsync(new List<DeviceType>());
        ApiMock.Setup(a => a.DeviceTypeServiceGet()).ReturnsAsync(new List<DeviceTypeService>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IApi>();
            services.AddSingleton(ApiMock.Object);

            services.AddAuthentication(TestRoleAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestRoleAuthHandler>(TestRoleAuthHandler.SchemeName, _ => { });
        });
    }

    public HttpClient CreateClientWithRoles(params string[] roles)
    {
        HttpClient client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add(TestRoleAuthHandler.RoleHeader, string.Join(';', roles));
        return client;
    }
}

/// A Global reader must see every setting and every button, but every Save/Delete/Add control must render disabled (never a working one); an admin sees the same controls, all enabled. ServerConfig/Users/Device-Edit stand in for the whole read-only surface.
public sealed class WebEndpointTests : IClassFixture<WebWebApplicationFactory>
{
    private readonly WebWebApplicationFactory _factory;

    public WebEndpointTests(WebWebApplicationFactory factory) => _factory = factory;

    /// A Global reader sees the same buttons/dialogs as anyone else (2026-09-13 policy: visible-but-disabled, not hidden) - so every submit/data-action tag must itself carry `disabled`, rather than being absent from the page entirely. A tag inside `<fieldset disabled>` has no `disabled` of its own, so those are matched too.
    private static void AssertNoWriteControls(string html, string label)
    {
        foreach (System.Text.RegularExpressions.Match tag in System.Text.RegularExpressions.Regex.Matches(html, "<[^>]+>"))
        {
            string t = tag.Value;
            bool isWriteControl = t.Contains("type=\"submit\"") || t.Contains("data-action=");
            if (!isWriteControl)
            {
                continue;
            }
            bool ownDisabled = t.Contains("disabled");
            bool insideDisabledFieldset = html.LastIndexOf("<fieldset disabled", tag.Index, StringComparison.Ordinal) >
                html.LastIndexOf("</fieldset>", tag.Index, StringComparison.Ordinal);
            Assert.True(ownDisabled || insideDisabledFieldset,
                $"{label}: expected every submit/data-action control to be disabled for a Global reader, but found an enabled one: {t}");
        }
    }

    private static void AssertHasWriteControls(string html, string label) =>
        Assert.True(html.Contains("type=\"submit\"") || html.Contains("data-action="),
            $"{label}: expected at least one submit button or data-action control for Global admin, found none.");

    [Theory]
    [InlineData("/ServerConfig")]
    [InlineData("/User")]
    [InlineData("/Device/Edit?idDevice=1")]
    [InlineData("/Device/Details?idDevice=1")]
    public async Task GlobalReader_SeesPage_WithNoWriteControls(string path)
    {
        using HttpClient client = _factory.CreateClientWithRoles(RoleNames.GlobalReader);

        HttpResponseMessage response = await client.GetAsync(path);
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == System.Net.HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {response.StatusCode}\n{body}");
        AssertNoWriteControls(body, path);
    }

    [Theory]
    [InlineData("/ServerConfig")]
    [InlineData("/User")]
    [InlineData("/Device/Edit?idDevice=1")]
    [InlineData("/Device/Details?idDevice=1")]
    public async Task GlobalAdmin_SeesPage_WithWriteControls(string path)
    {
        using HttpClient client = _factory.CreateClientWithRoles(RoleNames.GlobalAdmin);

        HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        AssertHasWriteControls(await response.Content.ReadAsStringAsync(), path);
    }
}
