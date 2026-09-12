using Agrumy.Api.Security;
using Agrumy.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Agrumy.Api.Startup
{
    /// JWT bearer auth for users plus the apiId/apiKey and session policies devices authenticate with.
    public static class AuthServiceExtensions
    {
        public static WebApplicationBuilder AddAgrumyAuth(this WebApplicationBuilder builder)
        {
            var secureKey = builder.Configuration["JWT:SecureKey"];
            if (string.IsNullOrEmpty(secureKey))
                throw new InvalidOperationException("JWT:SecureKey is missing in configuration.");

            var jwtIssuer = builder.Configuration["JWT:Issuer"];
            var jwtAudience = builder.Configuration["JWT:Audience"];
            if (string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
                throw new InvalidOperationException("JWT:Issuer and JWT:Audience are missing in configuration.");

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    // Same factory JwtTokenProvider.ValidateToken uses - see its BuildValidationParameters for why.
                    o.TokenValidationParameters = JwtTokenProvider.BuildValidationParameters(secureKey, jwtIssuer, jwtAudience);
                    // A JWT is self-validating and cannot be un-issued before its own expiry, so a password change or Enabled->false only takes effect immediately via this extra per-request check.
                    o.Events = new JwtBearerEvents { OnTokenValidated = TokenRevocationValidator.ValidateAsync };
                });

            // Device-communication endpoints authenticate by apiId/apiKey (or the short-lived apiAuth session token), not a user JWT - see Agrumy.Api.Security.DeviceAuth.
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(DeviceAuth.ApiKeyPolicy, p => p.AddRequirements(new DeviceApiKeyRequirement()));
                options.AddPolicy(DeviceAuth.SessionPolicy, p => p.AddRequirements(new DeviceSessionRequirement()));
            });
            builder.Services.AddScoped<IAuthorizationHandler, DeviceApiKeyHandler>();
            builder.Services.AddScoped<IAuthorizationHandler, DeviceSessionHandler>();

            return builder;
        }
    }
}
