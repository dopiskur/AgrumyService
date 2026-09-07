using Microsoft.AspNetCore.DataProtection;

namespace api.Security
{
    /// At-rest encryption for DB-stored secrets (broker/SMTP/WiFi passwords) - Protect on every write, Unprotect on every read.
    public interface ISecretProtector
    {
        string? Protect(string? plaintext);

        /// A value that fails to unprotect is treated as legacy plaintext from before this protector existed, returned as-is - re-encrypted the next time its row is written, no manual DB migration needed.
        string? Unprotect(string? storedValue);
    }

    public sealed class SecretProtector : ISecretProtector
    {
        private readonly IDataProtector protector;

        public SecretProtector(IDataProtectionProvider provider) =>
            protector = provider.CreateProtector("Agrumy.Api.Secrets.v1");

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
                return storedValue;
            }
        }
    }
}
