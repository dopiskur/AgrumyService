using Agrumy.Shared.Security;

namespace Agrumy.Api.Tests;

public class TokenRevocationCheckTests
{
    [Fact]
    public void IsRevoked_NoCutoff_NeverRevoked()
    {
        Assert.False(TokenRevocationCheck.IsRevoked(DateTime.UtcNow.AddDays(-1), null));
    }

    [Fact]
    public void IsRevoked_TokenIssuedBeforeCutoff_ReturnsTrue()
    {
        DateTime cutoff = DateTime.UtcNow;
        DateTime issuedAt = cutoff.AddMinutes(-5);
        Assert.True(TokenRevocationCheck.IsRevoked(issuedAt, cutoff));
    }

    [Fact]
    public void IsRevoked_TokenIssuedAfterCutoff_ReturnsFalse()
    {
        DateTime cutoff = DateTime.UtcNow;
        DateTime issuedAt = cutoff.AddMinutes(5);
        Assert.False(TokenRevocationCheck.IsRevoked(issuedAt, cutoff));
    }

    // The exact ForceChangePassword race: revoke writes a sub-second cutoff, then a token is issued moments later in the SAME second - JWT `iat` truncates to whole seconds, so without flooring the cutoff first, this compared as issued-before-cutoff and rejected the token the user just received.
    [Fact]
    public void IsRevoked_TokenIssuedSameSecondAsCutoff_ReturnsFalse()
    {
        var iat = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        var cutoff = new DateTimeOffset(2026, 9, 8, 12, 0, 0, 483, TimeSpan.Zero);
        Assert.False(TokenRevocationCheck.IsRevoked(iat, cutoff));
    }

    // A token genuinely issued before the cutoff's own second must still be caught - the fix only forgives same-second truncation, not real staleness.
    [Fact]
    public void IsRevoked_TokenIssuedSecondBeforeCutoffSecond_StillReturnsTrue()
    {
        var iat = new DateTimeOffset(2026, 9, 8, 11, 59, 59, TimeSpan.Zero);
        var cutoff = new DateTimeOffset(2026, 9, 8, 12, 0, 0, 483, TimeSpan.Zero);
        Assert.True(TokenRevocationCheck.IsRevoked(iat, cutoff));
    }

    // A cutoff with zero sub-second component (already whole-second) must behave identically to one with a fractional part - the floor is a no-op here.
    [Fact]
    public void IsRevoked_CutoffAlreadyWholeSecond_SameSecondToken_ReturnsFalse()
    {
        var iat = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        var cutoff = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        Assert.False(TokenRevocationCheck.IsRevoked(iat, cutoff));
    }
}
