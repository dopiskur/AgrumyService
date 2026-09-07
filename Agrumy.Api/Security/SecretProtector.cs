using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace api.Security
{
    /// At-rest encryption for DB-stored secrets (broker/SMTP/WiFi passwords) - Protect on every write, Unprotect on every read.
    public interface ISecretProtector
    {
        string? Protect(string? plaintext);

        /// A value that fails to unprotect is treated as legacy plaintext from before this protector existed, returned as-is - re-encrypted the next time its row is written, no manual DB migration needed.
        string? Unprotect(string? storedValue);
    }

    public sealed class SecretProtector(IDataProtectionProvider provider, ILogger<SecretProtector> logger) : ISecretProtector
    {
        private readonly IDataProtector protector = provider.CreateProtector("Agrumy.Api.Secrets.v1");

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
            catch (System.Security.Cryptography.CryptographicException)
            {
                // Indistinguishable from a genuine legacy-plaintext value at this point (both throw the same exception) - logged so a real cause (key rotated/corrupted, every stored secret now unreadable) is at least visible instead of silently masked as "must be legacy plaintext".
                logger.LogWarning("SecretProtector.Unprotect failed to decrypt a stored value - treating it as legacy plaintext. If this logs for a value that was previously encrypted successfully, the DataProtection key may have rotated or been lost.");
                return storedValue;
            }
        }
    }
}
