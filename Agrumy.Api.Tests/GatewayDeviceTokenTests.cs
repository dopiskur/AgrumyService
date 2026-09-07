using System.Security.Cryptography;
using System.Text;
using api.Security;

namespace Agrumy.Api.Tests;

public class GatewayDeviceTokenTests
{
    // Reimplements GatewayDeviceToken's private signing scheme so a past-expiry token can be constructed with an otherwise-valid signature - Issue() itself can only ever mint a future expiry.
    private static string SignedTokenWithExpiry(string apiId, string apiKey, long expiresAtUnixSeconds)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
        string signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{apiId}:{expiresAtUnixSeconds}")));
        return $"{expiresAtUnixSeconds}.{signature}";
    }

    [Fact]
    public void Verify_ExpiredToken_Fails()
    {
        long expiredOneHourAgo = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        string token = SignedTokenWithExpiry("dev1", "realApiKey", expiredOneHourAgo);

        Assert.False(GatewayDeviceToken.Verify(token, "dev1", "realApiKey"));
    }


    [Fact]
    public void Issue_ThenVerify_SameApiIdAndKey_Succeeds()
    {
        string token = GatewayDeviceToken.Issue("dev1", "realApiKey");

        Assert.True(GatewayDeviceToken.Verify(token, "dev1", "realApiKey"));
    }

    [Fact]
    public void Verify_WrongApiId_Fails()
    {
        string token = GatewayDeviceToken.Issue("dev1", "realApiKey");

        Assert.False(GatewayDeviceToken.Verify(token, "dev2", "realApiKey"));
    }

    [Fact]
    public void Verify_WrongApiKey_Fails()
    {
        string token = GatewayDeviceToken.Issue("dev1", "realApiKey");

        Assert.False(GatewayDeviceToken.Verify(token, "dev1", "wrongApiKey"));
    }

    [Fact]
    public void Verify_TamperedExpiry_Fails()
    {
        string token = GatewayDeviceToken.Issue("dev1", "realApiKey");
        int dot = token.IndexOf('.');
        // Extends the expiry without knowing the ApiKey - the signature was computed over the original expiry, so this must not verify.
        string tampered = (long.Parse(token[..dot]) + 3600) + token[dot..];

        Assert.False(GatewayDeviceToken.Verify(tampered, "dev1", "realApiKey"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("nodotatall")]
    [InlineData(".signatureonly")]
    public void Verify_MalformedToken_Fails(string malformed)
    {
        Assert.False(GatewayDeviceToken.Verify(malformed, "dev1", "realApiKey"));
    }

    [Fact]
    public void Verify_NullToken_Fails()
    {
        Assert.False(GatewayDeviceToken.Verify(null, "dev1", "realApiKey"));
    }

    [Fact]
    public void Verify_RawApiKeyPassedAsToken_Fails()
    {
        // A gateway that never picked up the new token shape (or an attacker who only has the real ApiKey, not a token) must not be able to pass it off as a token - GatewayApiController.RunEntryAsync checks the real ApiKey separately via ConstantTimeEquals, not through this path.
        Assert.False(GatewayDeviceToken.Verify("realApiKey", "dev1", "realApiKey"));
    }
}
