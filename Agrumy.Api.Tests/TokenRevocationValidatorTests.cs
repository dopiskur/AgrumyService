using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Agrumy.Api.Tests.TestSupport;

namespace Agrumy.Api.Tests;

/// ValidateAsync ran repo.UserGetAsync on every authenticated request; now it should hit the cache on a second call within the TTL instead of the DB again.
public class TokenRevocationValidatorTests
{
    private readonly Mock<IAllFacetsRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<ICache> _cache = new(MockBehavior.Strict);

    private TokenValidatedContext NewContext(string email, DateTime issuedAt)
    {
        var services = new ServiceCollection()
            .AddSingleton<IUserRepository>(_repo.Object)
            .AddSingleton(_cache.Object)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var options = new JwtBearerOptions();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, email)], JwtBearerDefaults.AuthenticationScheme, ClaimTypes.Name, null);

        var context = new TokenValidatedContext(httpContext, scheme, options)
        {
            Principal = new ClaimsPrincipal(identity),
            SecurityToken = new JwtSecurityToken(issuer: "test", audience: "test", claims: null, notBefore: null, expires: issuedAt.AddHours(1), signingCredentials: null),
        };
        return context;
    }

    [Fact]
    public async Task FirstCall_CacheMiss_QueriesRepoAndPopulatesCache()
    {
        DateTime issuedAt = DateTime.UtcNow;
        _cache.Setup(c => c.GetAsync<CachedRevocationState>("tokenRevocation:user@example.com")).ReturnsAsync((CachedRevocationState?)null);
        _repo.Setup(r => r.UserGetAsync(null, "user@example.com", null)).ReturnsAsync(new User { TokensValidAfterUtc = null });
        _cache.Setup(c => c.SetAsync("tokenRevocation:user@example.com", It.Is<CachedRevocationState>(s => s.TokensValidAfterUtc == null), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);

        var context = NewContext("user@example.com", issuedAt);
        await TokenRevocationValidator.ValidateAsync(context);

        Assert.True(context.Result?.Succeeded ?? true); // not failed
        _repo.Verify(r => r.UserGetAsync(null, "user@example.com", null), Times.Once);
    }

    [Fact]
    public async Task SecondCall_CacheHit_NeverQueriesRepoAgain()
    {
        DateTime issuedAt = DateTime.UtcNow;
        _cache.Setup(c => c.GetAsync<CachedRevocationState>("tokenRevocation:user@example.com"))
            .ReturnsAsync(new CachedRevocationState(null));

        var context = NewContext("user@example.com", issuedAt);
        await TokenRevocationValidator.ValidateAsync(context);

        // Strict mock: repo.UserGetAsync/cache.SetAsync were never set up - a cache hit must not touch either.
        Assert.True(context.Result?.Succeeded ?? true);
    }

    [Fact]
    public async Task CachedState_PasswordChangedAfterIssue_FailsEvenFromCache()
    {
        DateTime issuedAt = DateTime.UtcNow.AddMinutes(-10);
        _cache.Setup(c => c.GetAsync<CachedRevocationState>("tokenRevocation:user@example.com"))
            .ReturnsAsync(new CachedRevocationState(issuedAt.AddMinutes(5))); // password changed after the token was issued

        var context = NewContext("user@example.com", issuedAt);
        await TokenRevocationValidator.ValidateAsync(context);

        Assert.False(context.Result?.Succeeded ?? true);
    }
}
