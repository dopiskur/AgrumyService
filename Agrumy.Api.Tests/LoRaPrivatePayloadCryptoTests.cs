using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agrumy.Shared.LoRa;

namespace Agrumy.Api.Tests;

/// Covers Agrumy.Shared.LoRa.LoRaPrivatePayloadCrypto.Decrypt for both wire versions - v1 against a
/// fixture this test builds itself (AES-256-GCM, nonce = 4 zero bytes + 8-byte big-endian counter),
/// v2 against the SAME contracts/lora-private-v2.vectors.json AgrumyFirmware's native tests read (real
/// HKDF-SHA256 + AES-128-GCM output, not a fixture built with the production code's own logic - this
/// is a genuine independent-implementation cross-check, not a tautology). The actual over-the-air
/// round trip is untestable without hardware, same status as MqttCommandPublisherTests' own network call.
public class LoRaPrivatePayloadCryptoTests
{
    private static byte[] NewKey()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// Mirrors the v1 wire format Decrypt expects - not production code (only the firmware ever encrypts v1), just this test's own fixture builder.
    private static byte[] EncryptV1(byte[] key, ulong counter, string plaintext)
    {
        byte[] nonce = new byte[12];
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(4), counter);
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[16];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        byte[] wire = new byte[8 + ciphertext.Length + 16];
        BinaryPrimitives.WriteUInt64BigEndian(wire.AsSpan(0, 8), counter);
        ciphertext.CopyTo(wire, 8);
        tag.CopyTo(wire, 8 + ciphertext.Length);
        return wire;
    }

    [Fact]
    public void Decrypt_ValidCiphertext_ReturnsPlaintextAndCounter()
    {
        byte[] key = NewKey();
        byte[] wire = EncryptV1(key, 42, "{\"t\":\"sensor\",\"d\":[]}");

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Equal("{\"t\":\"sensor\",\"d\":[]}", plaintext);
        Assert.Equal(42UL, counter);
        Assert.Null(bootNonce);
    }

    [Fact]
    public void Decrypt_WrongKey_ReturnsNull()
    {
        byte[] wire = EncryptV1(NewKey(), 1, "hello");

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), wire);

        Assert.Null(plaintext);
        Assert.Equal(0UL, counter);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ReturnsNull_TagCheckFails()
    {
        byte[] key = NewKey();
        byte[] wire = EncryptV1(key, 1, "hello");
        wire[10] ^= 0xFF; // flip a byte inside the ciphertext region

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_TamperedCounter_ReturnsNull_BecauseItsAuthenticatedViaTheNonce()
    {
        // The counter feeds the nonce, so changing it without re-encrypting makes the tag check fail exactly like tampering the ciphertext - can't silently roll a counter back.
        byte[] key = NewKey();
        byte[] wire = EncryptV1(key, 5, "hello");
        BinaryPrimitives.WriteUInt64BigEndian(wire.AsSpan(0, 8), 1);

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_TooShort_ReturnsNull()
    {
        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), new byte[10]);

        Assert.Null(plaintext);
        Assert.Equal(0UL, counter);
    }

    [Fact]
    public void Decrypt_WrongKeyLength_ReturnsNull()
    {
        byte[] wire = EncryptV1(NewKey(), 1, "hello");

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(new byte[16], wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_EmptyPlaintext_StillRoundTrips()
    {
        byte[] key = NewKey();
        byte[] wire = EncryptV1(key, 1, "");

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Equal("", plaintext);
        Assert.Equal(1UL, counter);
    }

    [Fact]
    public void Decrypt_MalformedLeadingByte_ReturnsNull()
    {
        // Neither 0x00 (v1) nor 0x02 (v2) - must be rejected outright, not misparsed as either version.
        byte[] wire = { 0x01, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), wire);

        Assert.Null(plaintext);
    }

    public sealed record V2Vector(string MasterKeyHex, string BootNonceHex, uint Counter, string PlaintextUtf8, string SessionKeyHex, string FrameHex);

    public static IEnumerable<object[]> V2Vectors()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestVectors", "lora_private_v2_vectors.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement v in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            yield return new object[]
            {
                new V2Vector(
                    v.GetProperty("masterKeyHex").GetString()!,
                    v.GetProperty("bootNonceHex").GetString()!,
                    v.GetProperty("counter").GetUInt32(),
                    v.GetProperty("plaintextUtf8").GetString()!,
                    v.GetProperty("sessionKeyHex").GetString()!,
                    v.GetProperty("frameHex").GetString()!),
            };
        }
    }

    /// The vectors were generated independently in Python (cryptography library's HKDF + AESGCM), not with this
    /// production code - a match here means HKDF-SHA256(salt=bootNonce, info="agrumy-lora-v2", length=16) and the
    /// AES-128-GCM decrypt are both byte-for-byte correct, not just self-consistent.
    [Theory]
    [MemberData(nameof(V2Vectors))]
    public void Decrypt_V2SharedVector_MatchesIndependentlyComputedPlaintextAndBootNonce(V2Vector vector)
    {
        byte[] masterKey = Convert.FromHexString(vector.MasterKeyHex);
        byte[] expectedBootNonce = Convert.FromHexString(vector.BootNonceHex);
        byte[] frame = Convert.FromHexString(vector.FrameHex);

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(masterKey, frame);

        Assert.Equal(vector.PlaintextUtf8, plaintext);
        Assert.Equal((ulong)vector.Counter, counter);
        Assert.Equal(expectedBootNonce, bootNonce);
    }

    [Fact]
    public void Decrypt_V2_WrongMasterKey_ReturnsNull()
    {
        byte[] key = NewKey();
        byte[] bootNonce = RandomNumberGenerator.GetBytes(8);
        byte[] sessionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, key, 16, salt: bootNonce, info: "agrumy-lora-v2"u8.ToArray());
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("{\"battery\":50}");
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[16];
        byte[] nonce = new byte[12];
        bootNonce.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), 1);
        using (var gcm = new AesGcm(sessionKey, 16))
        {
            gcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }
        byte[] frame = new byte[1 + 8 + 4 + ciphertext.Length + 16];
        frame[0] = 0x02;
        bootNonce.CopyTo(frame, 1);
        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(9, 4), 1);
        ciphertext.CopyTo(frame, 13);
        tag.CopyTo(frame, 13 + ciphertext.Length);

        (string? plaintext, ulong counter, byte[]? bootNonceOut) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), frame);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_V2_TooShortForPrefixPlusTag_ReturnsNull()
    {
        byte[] wire = new byte[1 + 8 + 4 + 15]; // one byte short of the minimum (empty ciphertext + 16-byte tag)
        wire[0] = 0x02;

        (string? plaintext, ulong counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), wire);

        Assert.Null(plaintext);
    }
}
