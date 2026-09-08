namespace Agrumy.Shared.Security
{
    /// The DB-backed half of token revocation - a JWT is otherwise self-validating and cannot be un-issued before its own expiry, so this compares its issue time against a per-user cutoff bumped on password change or Enabled->false.
    public static class TokenRevocationCheck
    {
        /// The cutoff is floored to whole seconds before comparing - JWT `iat` (NumericDate) only ever carries second precision, but the cutoff is written with sub-second precision, so a token genuinely issued a few hundred ms AFTER the revocation but truncated into the SAME second would otherwise compare as issued-before-cutoff and be wrongly rejected on its very first use (a real, previously-live bug for ForceChangePassword/ChangePassword, which revoke then immediately issue a fresh token in one call).
        public static bool IsRevoked(DateTimeOffset tokenIssuedAtUtc, DateTimeOffset? tokensValidAfterUtc)
        {
            if (tokensValidAfterUtc is not DateTimeOffset cutoff)
            {
                return false;
            }
            DateTimeOffset flooredCutoff = new(cutoff.Year, cutoff.Month, cutoff.Day, cutoff.Hour, cutoff.Minute, cutoff.Second, cutoff.Offset);
            return tokenIssuedAtUtc < flooredCutoff;
        }
    }
}
