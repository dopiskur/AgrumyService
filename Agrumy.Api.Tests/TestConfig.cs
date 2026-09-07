using api;
using Microsoft.Extensions.Configuration;

namespace Agrumy.Api.Tests;

/// Shared appsettings.json binding for tests that need real JWT config (SecureKey/Issuer/Audience) -
/// reads the same file AgrumySettings.Bind(Configuration) uses in production, so a signed-then-validated
/// JWT round-trip in a test uses the same values JwtTokenProvider's explicit parameters would in a host.
internal static class TestConfig
{
    public static readonly IConfigurationRoot Configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build();

    public static readonly AgrumySettings Settings = AgrumySettings.Bind(Configuration);
}
