using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Agrumy.Shared.LoRa
{
    /// AES-128-GCM confidentiality+integrity for LoRa private-protocol uplinks - the physical gateway (serial-bridge or WiFi-relay) only ever forwards these bytes verbatim and never holds the key (only the leaf node, see AgrumyFirmware's LoRaPrivateController, and this decrypt path do); wire format is 0x02 || bootNonce:8 bytes || counter:4 bytes big-endian || ciphertext || tag:16 bytes under a per-boot session key = HKDF-SHA256(salt=bootNonce, ikm=masterKey, info="agrumy-lora-v2", length=16) with nonce = bootNonce(8) || counter(4, big-endian) for 12 bytes total, and since the device never persists a counter, replay protection is "this (bootNonce, counter) pair was never seen before" (see EfDeviceRepository.DeviceLoRaSessionAcceptAsync) rather than "counter only ever grows" - any leading byte other than 0x02 is malformed and rejected outright.
    public static class LoRaPrivatePayloadCrypto
    {
        public const int KeyLength = 32;
        private const int TagLength = 16;
        private const int NonceLength = 12;
        private const byte VersionByte = 0x02;
        private const int BootNonceLength = 8;
        private const int CounterLength = 4;
        private const int SessionKeyLength = 16;
        private static readonly byte[] HkdfInfo = Encoding.ASCII.GetBytes("agrumy-lora-v2");

        /// Null Plaintext (Counter/BootNonce meaningless) on anything malformed or on a failed auth-tag
        /// check (wrong key, corrupted or tampered bytes) - never trust Counter without a successful decrypt.
        public static (string? Plaintext, uint Counter, byte[]? BootNonce) Decrypt(byte[] key, ReadOnlySpan<byte> wirePayload)
        {
            int prefixLength = 1 + BootNonceLength + CounterLength;
            if (key.Length != KeyLength || wirePayload.Length < prefixLength + TagLength || wirePayload[0] != VersionByte)
            {
                return (null, 0, null);
            }

            byte[] bootNonce = wirePayload.Slice(1, BootNonceLength).ToArray();
            uint counter = BinaryPrimitives.ReadUInt32BigEndian(wirePayload.Slice(1 + BootNonceLength, CounterLength));
            int cipherLength = wirePayload.Length - prefixLength - TagLength;
            ReadOnlySpan<byte> ciphertext = wirePayload.Slice(prefixLength, cipherLength);
            ReadOnlySpan<byte> tag = wirePayload.Slice(prefixLength + cipherLength, TagLength);

            Span<byte> nonce = stackalloc byte[NonceLength];
            bootNonce.CopyTo(nonce);
            BinaryPrimitives.WriteUInt32BigEndian(nonce[BootNonceLength..], counter);

            byte[] sessionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, key, SessionKeyLength, salt: bootNonce, info: HkdfInfo);
            string? plaintext = TryDecrypt(sessionKey, nonce, ciphertext, tag);
            return plaintext is null ? (null, 0, null) : (plaintext, counter, bootNonce);
        }

        /// Builds a wire frame identical in shape to what AgrumyFirmware's LoRaPrivateController sends - kept for tests and any future device simulator, never called from the real decrypt path.
        public static byte[] Encrypt(byte[] key, byte[] bootNonce, uint counter, string plaintext)
        {
            Span<byte> nonce = stackalloc byte[NonceLength];
            bootNonce.CopyTo(nonce);
            BinaryPrimitives.WriteUInt32BigEndian(nonce[BootNonceLength..], counter);

            byte[] sessionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, key, SessionKeyLength, salt: bootNonce, info: HkdfInfo);
            byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] ciphertext = new byte[plaintextBytes.Length];
            byte[] tag = new byte[TagLength];
            using (var gcm = new AesGcm(sessionKey, TagLength))
            {
                gcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
            }

            byte[] wire = new byte[1 + BootNonceLength + CounterLength + ciphertext.Length + TagLength];
            wire[0] = VersionByte;
            bootNonce.CopyTo(wire, 1);
            BinaryPrimitives.WriteUInt32BigEndian(wire.AsSpan(1 + BootNonceLength, CounterLength), counter);
            ciphertext.CopyTo(wire, 1 + BootNonceLength + CounterLength);
            tag.CopyTo(wire, 1 + BootNonceLength + CounterLength + ciphertext.Length);
            return wire;
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
