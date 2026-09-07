using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Agrumy.Shared.LoRa
{
    /// AES-256-GCM confidentiality+integrity for LoRa private-protocol uplinks (roadmap #395) - the
    /// physical gateway (serial-bridge or WiFi-relay) only ever forwards these bytes verbatim, it
    /// never holds the key; only the leaf node (encrypts, see AgrumyFirmware's LoRaPrivateController)
    /// and this decrypt path (server-side, both relay paths) do.
    ///
    /// Wire format of the encrypted payload: [counter:8 bytes big-endian][ciphertext][tag:16 bytes].
    /// Nonce (12 bytes, GCM's required length) = 4 zero bytes + the 8-byte counter - unique per key
    /// as long as counter never repeats under the same key, which the caller enforces by rejecting
    /// any counter no higher than the last one seen for that device (also the replay-protection rule
    /// roadmap #395 asks for, not a separate mechanism).
    public static class LoRaPrivatePayloadCrypto
    {
        public const int KeyLength = 32;
        private const int CounterLength = 8;
        private const int TagLength = 16;
        private const int NonceLength = 12;

        /// Null Plaintext (Counter is meaningless, 0) on anything malformed or on a failed auth-tag
        /// check (wrong key, corrupted or tampered bytes) - never trust Counter without a successful decrypt.
        public static (string? Plaintext, ulong Counter) Decrypt(byte[] key, ReadOnlySpan<byte> wirePayload)
        {
            if (key.Length != KeyLength || wirePayload.Length < CounterLength + TagLength)
            {
                return (null, 0);
            }

            ulong counter = BinaryPrimitives.ReadUInt64BigEndian(wirePayload[..CounterLength]);
            int cipherLength = wirePayload.Length - CounterLength - TagLength;
            ReadOnlySpan<byte> ciphertext = wirePayload.Slice(CounterLength, cipherLength);
            ReadOnlySpan<byte> tag = wirePayload.Slice(CounterLength + cipherLength, TagLength);

            Span<byte> nonce = stackalloc byte[NonceLength];
            BinaryPrimitives.WriteUInt64BigEndian(nonce[4..], counter);

            byte[] plaintext = new byte[cipherLength];
            try
            {
                using var gcm = new AesGcm(key, TagLength);
                gcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            catch (CryptographicException)
            {
                return (null, 0);
            }

            return (Encoding.UTF8.GetString(plaintext), counter);
        }
    }
}
