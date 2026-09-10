using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Agrumy.Shared.LoRa
{
    /// AES-GCM confidentiality+integrity for LoRa private-protocol uplinks - the physical gateway
    /// (serial-bridge or WiFi-relay) only ever forwards these bytes verbatim, it never holds the key;
    /// only the leaf node (encrypts, see AgrumyFirmware's LoRaPrivateController) and this decrypt path
    /// (server-side, both relay paths) do.
    ///
    /// Two wire formats coexist during the rollout to per-boot session keys, disambiguated by the leading byte:
    ///
    /// v1 = [counter:8 bytes big-endian][ciphertext][tag:16 bytes], AES-256-GCM directly
    /// under the device's master key. Nonce = 4 zero bytes + the 8-byte counter. A v1 counter is small
    /// enough in practice (one increment per uplink, persisted, never resets) that its leading byte is
    /// always 0x00 - that's the v1 marker, not a separate version tag ever added to the wire format.
    ///
    /// v2 = 0x02 || bootNonce:8 bytes || counter:4 bytes big-endian || ciphertext ||
    /// tag:16 bytes, AES-128-GCM under a per-boot session key = HKDF-SHA256(salt=bootNonce,
    /// ikm=masterKey, info="agrumy-lora-v2", length=16) - the device never persists a counter, so
    /// replay protection is "this (bootNonce, counter) pair was never seen before" (see
    /// EfDeviceRepository.DeviceLoRaSessionAcceptAsync) instead of "counter only ever grows".
    /// Nonce = bootNonce(8) || counter(4, big-endian), 12 bytes total either way.
    ///
    /// Any other leading byte is treated as malformed and rejected outright.
    public static class LoRaPrivatePayloadCrypto
    {
        public const int KeyLength = 32;
        private const int V1CounterLength = 8;
        private const int TagLength = 16;
        private const int NonceLength = 12;
        private const byte V1LeadingByte = 0x00;
        private const byte V2VersionByte = 0x02;
        private const int V2BootNonceLength = 8;
        private const int V2CounterLength = 4;
        private const int V2SessionKeyLength = 16;
        private static readonly byte[] V2HkdfInfo = Encoding.ASCII.GetBytes("agrumy-lora-v2");

        /// Null Plaintext (Counter/BootNonce meaningless) on anything malformed or on a failed auth-tag
        /// check (wrong key, corrupted or tampered bytes) - never trust Counter without a successful
        /// decrypt. BootNonce is null for a v1 frame, 8 bytes for v2 - the caller's replay-check
        /// branches on its presence (GatewayApiController.RelayUplink).
        public static (string? Plaintext, ulong Counter, byte[]? BootNonce) Decrypt(byte[] key, ReadOnlySpan<byte> wirePayload)
        {
            if (key.Length != KeyLength || wirePayload.Length < 1)
            {
                return (null, 0, null);
            }

            return wirePayload[0] switch
            {
                V2VersionByte => DecryptV2(key, wirePayload),
                V1LeadingByte => DecryptV1(key, wirePayload),
                _ => (null, 0, null),
            };
        }

        private static (string? Plaintext, ulong Counter, byte[]? BootNonce) DecryptV1(byte[] key, ReadOnlySpan<byte> wirePayload)
        {
            if (wirePayload.Length < V1CounterLength + TagLength)
            {
                return (null, 0, null);
            }

            ulong counter = BinaryPrimitives.ReadUInt64BigEndian(wirePayload[..V1CounterLength]);
            int cipherLength = wirePayload.Length - V1CounterLength - TagLength;
            ReadOnlySpan<byte> ciphertext = wirePayload.Slice(V1CounterLength, cipherLength);
            ReadOnlySpan<byte> tag = wirePayload.Slice(V1CounterLength + cipherLength, TagLength);

            Span<byte> nonce = stackalloc byte[NonceLength];
            BinaryPrimitives.WriteUInt64BigEndian(nonce[4..], counter);

            string? plaintext = TryDecrypt(key, nonce, ciphertext, tag);
            return plaintext is null ? (null, 0, null) : (plaintext, counter, null);
        }

        private static (string? Plaintext, ulong Counter, byte[]? BootNonce) DecryptV2(byte[] key, ReadOnlySpan<byte> wirePayload)
        {
            int prefixLength = 1 + V2BootNonceLength + V2CounterLength;
            if (wirePayload.Length < prefixLength + TagLength)
            {
                return (null, 0, null);
            }

            byte[] bootNonce = wirePayload.Slice(1, V2BootNonceLength).ToArray();
            uint counter = BinaryPrimitives.ReadUInt32BigEndian(wirePayload.Slice(1 + V2BootNonceLength, V2CounterLength));
            int cipherLength = wirePayload.Length - prefixLength - TagLength;
            ReadOnlySpan<byte> ciphertext = wirePayload.Slice(prefixLength, cipherLength);
            ReadOnlySpan<byte> tag = wirePayload.Slice(prefixLength + cipherLength, TagLength);

            Span<byte> nonce = stackalloc byte[NonceLength];
            bootNonce.CopyTo(nonce);
            BinaryPrimitives.WriteUInt32BigEndian(nonce[V2BootNonceLength..], counter);

            byte[] sessionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, key, V2SessionKeyLength, salt: bootNonce, info: V2HkdfInfo);
            string? plaintext = TryDecrypt(sessionKey, nonce, ciphertext, tag);
            return plaintext is null ? (null, 0, null) : (plaintext, counter, bootNonce);
        }

        private static string? TryDecrypt(byte[] key, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> tag)
        {
            byte[] plaintext = new byte[ciphertext.Length];
            try
            {
                using var gcm = new AesGcm(key, TagLength);
                gcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            catch (CryptographicException)
            {
                return null;
            }
            return Encoding.UTF8.GetString(plaintext);
        }
    }
}
