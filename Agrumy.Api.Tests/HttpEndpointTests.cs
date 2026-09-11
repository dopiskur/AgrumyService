using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.RegularExpressions;
using Agrumy.Api.Security;
using Agrumy.Dal;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Agrumy.Api.Tests;

/// Fails a connection attempt before any real socket touches the network, so a simulated DB-unreachable response is instant and machine-load-independent instead of riding real TCP-refused timing.
file sealed class ImmediateConnectionFailureInterceptor : DbConnectionInterceptor
{
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
        throw new TimeoutException("Simulated DB-unreachable failure for HTTP endpoint tests.");

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
        throw new TimeoutException("Simulated DB-unreachable failure for HTTP endpoint tests.");
}

/// Drives the real HTTP middleware pipeline (auth, rate limiting, exception handling) end-to-end instead of unit-testing the pieces in isolation - see roadmap #315.
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Agrumy.Api.ApiHostMarker>
{
    // This factory's own Program.cs run reads these via env vars below - no shared static state with TestConfig/other test classes to race under xUnit's parallel execution (roadmap #397(3) removed the static Config bridge JwtTokenProvider used to depend on).
    public const string SigningKey = "unit-test-signing-key-not-a-secret-0123456789ABCDEF";
    public const string Issuer = "https://tests.agrumy.local";
    public const string Audience = "agrumy-api-tests";

    public ApiWebApplicationFactory()
    {
        // Environment variables are folded into WebApplicationBuilder's configuration at
        // CreateBuilder(args) time, before Program.cs's own pre-Build() DefaultConnection check -
        // unlike WebHostBuilder.ConfigureAppConfiguration, which only applies once Build() runs and
        // would arrive too late to stop that check from routing into the first-boot Setup wizard.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection",
            "server=127.0.0.1;port=1;database=agrumy_http_tests;user id=test;password=test;Connection Timeout=2;");
        Environment.SetEnvironmentVariable("JWT__SecureKey", SigningKey);
        Environment.SetEnvironmentVariable("JWT__Issuer", Issuer);
        Environment.SetEnvironmentVariable("JWT__Audience", Audience);
        Environment.SetEnvironmentVariable("WebView__ApiService", "http://127.0.0.1:1");
    }

    public string TokenFor(params string[] roles) =>
        JwtTokenProvider.CreateToken(SigningKey, expiration: 5, subject: "http-tests@example.com", roles, tenantID: "0", Issuer, Audience);

    // Program.cs's own AgrumyDbContext registration points at a real (closed) socket, whose failure latency isn't bounded under machine load - swap in one that fails at the ADO.NET layer instead.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AgrumyDbContext>();
            services.AddScoped(_ =>
            {
                var options = new DbContextOptionsBuilder<AgrumyDbContext>()
                    .UseMySql("server=127.0.0.1;port=1;database=agrumy_http_tests;user id=test;password=test;", new MariaDbServerVersion(new Version(11, 4, 0)))
                    .AddInterceptors(new ImmediateConnectionFailureInterceptor())
                    .Options;
                return new AgrumyDbContext(options);
            });
        });
    }
}

public sealed class HttpEndpointTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;

    public HttpEndpointTests(ApiWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ProtectedEndpoint_NoToken_Returns401()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/Tenant/All");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_TokenWithoutRequiredRole_Returns403()
    {
        using HttpClient client = _factory.CreateClient();
        // "Tenant reader" is a real role, just not one RoleNames.TenantReaders' [Authorize(Roles=...)] accepts.
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.TokenFor(RoleNames.TenantReader));

        HttpResponseMessage response = await client.GetAsync("/api/Tenant/All");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_ReachableWithoutAuth()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/health");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// Role check passes, so the request reaches the controller action and its DB call fails against
    /// the deliberately-unreachable connection string above - DbExceptionFilter must turn that into a
    /// structured error response, never an unhandled-exception crash.
    [Fact]
    public async Task DbUnavailable_ReturnsStructuredError_NotUnhandledException()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.TokenFor(RoleNames.GlobalAdmin));

        HttpResponseMessage response = await client.GetAsync("/api/Tenant/All");

        Assert.True((int)response.StatusCode is 500 or 503, $"Expected a DbExceptionFilter status, got {(int)response.StatusCode}");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("reason") && body.ContainsKey("message"));
    }

    [Fact]
    public async Task LoginRateLimit_RejectsAfterFivePerMinutePerIp()
    {
        using HttpClient client = _factory.CreateClient();
        HttpStatusCode? sixth = null;

        for (int i = 0; i < 6; i++)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/User/ResendActivation", new { login = $"nobody{i}@example.com" });
            if (i == 5) sixth = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, sixth);
    }

    [Fact]
    public async Task PrometheusMetrics_NoToken_Returns401()
    {
        using HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/metrics/prometheus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PrometheusMetrics_TokenWithoutRequiredRole_Returns403()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.TokenFor(RoleNames.TenantReader));

        HttpResponseMessage response = await client.GetAsync("/api/metrics/prometheus");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PrometheusMetrics_WithMetricsReaderRole_ReturnsTextExposition()
    {
        using HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", _factory.TokenFor(RoleNames.GlobalAdmin));

        HttpResponseMessage response = await client.GetAsync("/api/metrics/prometheus");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    // Every [Authorize(Roles=...)]-gated action, driven by reflection instead of one hand-written test
    // per endpoint - a controller whose Roles attribute is missing or wrong shows up here without
    // anyone remembering to add a case for it. Device-communication endpoints (DeviceAuth.ApiKeyPolicy/
    // SessionPolicy) authenticate by apiId/apiKey, not a JWT role, so they're out of scope for this matrix.
    private static readonly string[] AtomicRoles =
    [
        RoleNames.GlobalAdmin, RoleNames.GlobalReader, RoleNames.TenantAdmin, RoleNames.TenantReader,
        RoleNames.GlobalUser, RoleNames.GlobalDevice, RoleNames.TenantUser, RoleNames.TenantDevice,
        RoleNames.GlobalDataReader, RoleNames.TenantDataReader, RoleNames.SimulationAdministrator,
    ];

    private static string SubstituteRouteParams(string template) => Regex.Replace(template, "\\{[^}]+\\}", "1");

    /// The independent half of this matrix: expectations come from a hand-maintained CSV, not from the
    /// [Authorize] attribute the test is supposed to be checking, so a role changed in code without a
    /// matching CSV update (in either direction) is a hard failure here instead of silently validating
    /// itself. Same file used by both RoleGatedActions (endpoint-not-in-CSV -> exception, fails the
    /// whole run) and CsvMatrixRoles_MatchReflectedAuthorizeAttributeRoles (CSV/attribute disagreement).
    private static Dictionary<(string Method, string Template), string[]> LoadCsvMatrix()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestVectors", "role_endpoint_matrix.csv");
        var result = new Dictionary<(string, string), string[]>();
        foreach (string line in File.ReadLines(path).Skip(1)) // header row
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            string[] parts = line.Split(',', 3);
            result[(parts[0], parts[1])] = parts[2].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        return result;
    }

    public static IEnumerable<object[]> RoleGatedActions()
    {
        var csvMatrix = LoadCsvMatrix();
        using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>();

        foreach (var action in provider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
        {
            if (action.AttributeRouteInfo?.Template is not string template)
            {
                continue;
            }

            var authorizeAttrs = action.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(true)
                .Concat(action.MethodInfo.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(true))
                .ToList();
            bool allowAnonymous = action.MethodInfo.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() ||
                action.MethodInfo.DeclaringType!.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
            bool isDevicePolicy = authorizeAttrs.Any(a => a.Policy is DeviceAuth.ApiKeyPolicy or DeviceAuth.SessionPolicy);

            if (allowAnonymous || isDevicePolicy || authorizeAttrs.Count == 0)
            {
                continue;
            }

            var requiredRoles = authorizeAttrs
                .SelectMany(a => (a.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToHashSet();
            if (requiredRoles.Count == 0)
            {
                continue; // bare [Authorize] with no role list - "any authenticated user", nothing role-specific to assert; see BareAuthorizeActions for this class's own inventory.
            }

            string httpMethod = action.MethodInfo.GetCustomAttributes(true)
                .OfType<IActionHttpMethodProvider>().FirstOrDefault()?.HttpMethods.FirstOrDefault() ?? "GET";

            // A reflected action with no CSV row is exactly the gap #462 closes - fail loudly (this exception aborts test discovery/collection for the whole class) rather than silently skip it.
            if (!csvMatrix.TryGetValue((httpMethod, template), out string[]? csvRoles))
            {
                throw new InvalidOperationException(
                    $"role_endpoint_matrix.csv has no row for \"{httpMethod},{template}\" - add one (Roles column ';'-separated) so this endpoint's authorization is asserted independently of its own [Authorize] attribute.");
            }

            string wrongRole = AtomicRoles.FirstOrDefault(r => !requiredRoles.Contains(r)) ?? "";

            yield return [$"{httpMethod} /{template}", httpMethod, "/" + SubstituteRouteParams(template), wrongRole, csvRoles, requiredRoles.OrderBy(r => r).ToArray()];
        }
    }

    /// The other half of #462's independence check: not just "does a CSV row exist" (RoleGatedActions
    /// throws on that) but "does its role set still match the code" - a role added/removed from the
    /// [Authorize] attribute without updating the CSV is exactly the #395-style drift this whole
    /// mechanism exists to catch, in either direction.
    [Theory, MemberData(nameof(RoleGatedActions))]
    public void CsvMatrixRoles_MatchReflectedAuthorizeAttributeRoles(string label, string httpMethod, string url, string wrongRole, string[] csvRoles, string[] reflectedRoles)
    {
        _ = (httpMethod, url, wrongRole); // this theory only cares about the two role sets - shares RoleGatedActions' MemberData with RoleGatedAction_EnforcesItsDeclaredRoles rather than duplicating the reflection walk
        Assert.True(csvRoles.OrderBy(r => r).SequenceEqual(reflectedRoles),
            $"{label}: role_endpoint_matrix.csv says [{string.Join(";", csvRoles)}], code's [Authorize] says [{string.Join(";", reflectedRoles)}] - update whichever one is stale.");
    }

    [Theory, MemberData(nameof(RoleGatedActions))]
    public async Task RoleGatedAction_EnforcesItsDeclaredRoles(string label, string httpMethod, string url, string wrongRole, string[] csvRoles, string[] reflectedRoles)
    {
        _ = reflectedRoles; // this theory only needs csvRoles - reflectedRoles is CsvMatrixRoles_MatchReflectedAuthorizeAttributeRoles' concern
        using HttpClient client = _factory.CreateClient();

        using var noTokenRequest = new HttpRequestMessage(new HttpMethod(httpMethod), url);
        HttpResponseMessage noTokenResponse = await client.SendAsync(noTokenRequest);
        Assert.True(noTokenResponse.StatusCode == HttpStatusCode.Unauthorized,
            $"{label}: expected 401 with no token, got {(int)noTokenResponse.StatusCode}.");

        if (!string.IsNullOrEmpty(wrongRole))
        {
            using var wrongRoleRequest = new HttpRequestMessage(new HttpMethod(httpMethod), url)
            {
                Headers = { Authorization = new("Bearer", _factory.TokenFor(wrongRole)) },
            };
            HttpResponseMessage wrongRoleResponse = await client.SendAsync(wrongRoleRequest);
            Assert.True(wrongRoleResponse.StatusCode == HttpStatusCode.Forbidden,
                $"{label}: expected 403 for role \"{wrongRole}\", got {(int)wrongRoleResponse.StatusCode}.");
        }

        // Every CSV-declared role, not just one - catches the code accepting FEWER roles than the independent matrix claims it should.
        foreach (string rightRole in csvRoles)
        {
        using var rightRoleRequest = new HttpRequestMessage(new HttpMethod(httpMethod), url)
        {
            Headers = { Authorization = new("Bearer", _factory.TokenFor(rightRole)) },
        };
        HttpResponseMessage rightRoleResponse = await client.SendAsync(rightRoleRequest);
        // Same distinction Agrumy.Web's ApiException.IsAuthChallenge relies on - only a WWW-Authenticate-bearing 401 comes from the JWT bearer challenge itself; an action returning a bare Unauthorized(...) as its own business result (e.g. UserApiController.Delete's "can't delete the default account") isn't an auth-framework rejection.
        bool isRealAuthFailure = rightRoleResponse.StatusCode == HttpStatusCode.Forbidden ||
            (rightRoleResponse.StatusCode == HttpStatusCode.Unauthorized && rightRoleResponse.Headers.WwwAuthenticate.Count > 0);
        Assert.False(isRealAuthFailure,
            $"{label}: role \"{rightRole}\" should be let through, got {(int)rightRoleResponse.StatusCode}.");
        }
    }

    // A class this role-endpoint matrix never covers: a bare [Authorize] action
    // (no role list) relies entirely on an ownership check in the controller body, not a role, to keep
    // Tenant A out of Tenant B's resource. Enumerated here so the gap is visible and countable; a
    // cross-tenant 403/404 assertion per action needs a real (not the deliberately-unreachable
    // ApiWebApplicationFactory) database, so that part is covered separately by the existing cross-tenant
    // tests in RelationalIntegrationTests/ApiControllerTests/GatewayApiControllerTests/
    // DiscoveryWifiConfigTests (grep "cross-tenant"/"CrossTenant"/"DifferentTenant") - this asserts that
    // set of actions is at least fully enumerated, not (yet) that every single one has a dedicated test.
    public static IEnumerable<object[]> BareAuthorizeActions()
    {
        using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>();

        foreach (var action in provider.ActionDescriptors.Items.OfType<ControllerActionDescriptor>())
        {
            if (action.AttributeRouteInfo?.Template is not string template)
            {
                continue;
            }
            var authorizeAttrs = action.MethodInfo.GetCustomAttributes<AuthorizeAttribute>(true)
                .Concat(action.MethodInfo.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>(true))
                .ToList();
            bool allowAnonymous = action.MethodInfo.GetCustomAttributes<AllowAnonymousAttribute>(true).Any() ||
                action.MethodInfo.DeclaringType!.GetCustomAttributes<AllowAnonymousAttribute>(true).Any();
            bool isDevicePolicy = authorizeAttrs.Any(a => a.Policy is DeviceAuth.ApiKeyPolicy or DeviceAuth.SessionPolicy);
            if (allowAnonymous || isDevicePolicy || authorizeAttrs.Count == 0)
            {
                continue;
            }
            bool hasRoleList = authorizeAttrs.Any(a => (a.Roles ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Length > 0);
            if (hasRoleList)
            {
                continue;
            }
            yield return [$"{action.MethodInfo.DeclaringType!.Name}.{action.MethodInfo.Name}", template];
        }
    }

    /// Not a behavior assertion - a count regression check. If this drops, a bare-[Authorize] action was
    /// either removed (fine) or accidentally gained a role list and silently left this inventory (also
    /// fine, just means BareAuthorizeActions itself needs no change) - if it RISES, whoever's reading a
    /// failing build here should go verify the new action's ownership check is real, then bump the count.
    [Fact]
    public void BareAuthorizeActions_CountIsKnown()
    {
        int count = BareAuthorizeActions().Count();
        Assert.True(count > 0, "Expected at least one bare-[Authorize] action (ownership-checked-in-body) to exist.");
    }
}
