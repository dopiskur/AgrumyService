using Agrumy.Shared.Security;
using System.IdentityModel.Tokens.Jwt;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Agrumy.Api.Security
{
    /// Just the field this check actually needs - deliberately not the full User (PwdHash/PwdSalt have no business sitting in the cache backing store for this).
    internal sealed record CachedRevocationState(DateTimeOffset? TokensValidAfterUtc);

    /// AddJwtBearer's OnTokenValidated hook - rejects a structurally valid, unexpired token if the caller's password changed or account was disabled after it was issued. See Agrumy.Shared.Security.TokenRevocationCheck for the actual decision.
    public static class TokenRevocationValidator
    {
        public static async Task ValidateAsync(TokenValidatedContext context)
        {
            if (context.Principal?.Identity?.Name is not string email ||
                context.SecurityToken is not JwtSecurityToken jwt)
            {
                return;
            }

            ICache cache = context.HttpContext.RequestServices.GetRequiredService<ICache>();
            string cacheKey = Agrumy.Api.Dal.CacheKeys.TokenRevocation(email);
            CachedRevocationState? state = await cache.GetAsync<CachedRevocationState>(cacheKey);
            if (state is null)
            {
                IRepository repo = context.HttpContext.RequestServices.GetRequiredService<IRepository>();
                User? user = await repo.UserGetAsync(null, email, null);
                if (user is null)
                {
                    return; // unreachable for a token that passed signature validation, but nothing to cache either way
                }
                state = new CachedRevocationState(user.TokensValidAfterUtc);
                await cache.SetAsync(cacheKey, state, Agrumy.Api.Dal.CacheKeys.TokenRevocationTtl);
            }

            if (TokenRevocationCheck.IsRevoked(DateTime.SpecifyKind(jwt.IssuedAt, DateTimeKind.Utc), state.TokensValidAfterUtc))
            {
                context.Fail("Token revoked - password changed or account disabled.");
            }
        }
    }
}
