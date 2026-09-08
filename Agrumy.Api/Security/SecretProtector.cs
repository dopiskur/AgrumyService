using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Agrumy.Api.Security
{
    /// At-rest encryption for DB-stored secrets (broker/SMTP/WiFi passwords) - Protect on every write, Unprotect on every read.
    public interface ISecretProtector
    {
        string? Protect(string? plaintext);

        /// A value that fails to unprotect is treated as legacy plaintext from before this protector existed, returned as-is - re-encrypted the next time its row is written, no manual DB migration needed. A value that carries the DataProtection ciphertext prefix but still fails to unprotect is a real decrypt failure (rotated/lost key), never returned as if it were plaintext.
        string? Unprotect(string? storedValue);
    }

    public sealed class SecretProtector(IDataProtectionProvider provider, ILogger<SecretProtector> logger) : ISecretProtector
    {
        private readonly IDataProtector protector = provider.CreateProtector("Agrumy.Api.Secrets.v1");

        // Base64 encoding of the DataProtection payload's fixed 4-byte magic header - a value with this prefix is provably ciphertext, never legacy plaintext.
        private const string CiphertextPrefix = "CfDJ8";

        public string? Protect(string? plaintext) =>
            string.IsNullOrEmpty(plaintext) ? plaintext : protector.Protect(plaintext);

        public string? Unprotect(string? storedValue)
        {
            if (string.IsNullOrEmpty(storedValue))
            {
                return storedValue;
            }
            try
            {
                return protector.Unprotect(storedValue);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                if (storedValue.StartsWith(CiphertextPrefix, StringComparison.Ordinal))
                {
                    logger.LogError(ex, "SecretProtector.Unprotect failed to decrypt a value that carries the DataProtection ciphertext prefix - the key was likely rotated or lost, refusing to return raw ciphertext as if it were plaintext.");
                    throw;
                }

                // No ciphertext prefix, so this is genuine legacy plaintext from before this protector existed.
                logger.LogWarning("SecretProtector.Unprotect failed to decrypt a stored value - treating it as legacy plaintext.");
                return storedValue;
            }
        }
    }
}
