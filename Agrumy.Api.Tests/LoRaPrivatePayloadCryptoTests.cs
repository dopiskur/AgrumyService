using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agrumy.Shared.LoRa;

namespace Agrumy.Api.Tests;

/// Covers Agrumy.Shared.LoRa.LoRaPrivatePayloadCrypto.Decrypt against the SAME contracts/lora-private-v2.vectors.json AgrumyFirmware's native tests read (a genuine independent-implementation cross-check, not a tautology) plus Encrypt-then-Decrypt round trips for cases the shared vectors don't cover - the actual over-the-air round trip stays untestable without hardware, same status as MqttCommandPublisherTests' own network call.
public class LoRaPrivatePayloadCryptoTests
{
    private static byte[] NewKey()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    private static byte[] NewBootNonce()
    {
        byte[] bootNonce = new byte[8];
        RandomNumberGenerator.Fill(bootNonce);
        return bootNonce;
    }

    [Fact]
    public void Decrypt_ValidCiphertext_ReturnsPlaintextAndCounter()
    {
        byte[] key = NewKey();
        byte[] bootNonce = NewBootNonce();
        byte[] wire = LoRaPrivatePayloadCrypto.Encrypt(key, bootNonce, 42, "{\"t\":\"sensor\",\"d\":[]}");

        (string? plaintext, uint counter, byte[]? decodedBootNonce) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Equal("{\"t\":\"sensor\",\"d\":[]}", plaintext);
        Assert.Equal(42U, counter);
        Assert.Equal(bootNonce, decodedBootNonce);
    }

    [Fact]
    public void Decrypt_WrongKey_ReturnsNull()
    {
        byte[] wire = LoRaPrivatePayloadCrypto.Encrypt(NewKey(), NewBootNonce(), 1, "hello");

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), wire);

        Assert.Null(plaintext);
        Assert.Equal(0U, counter);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ReturnsNull_TagCheckFails()
    {
        byte[] key = NewKey();
        byte[] wire = LoRaPrivatePayloadCrypto.Encrypt(key, NewBootNonce(), 1, "hello");
        wire[^1] ^= 0xFF; // flip a byte inside the tag

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_TamperedCounter_ReturnsNull_BecauseItsAuthenticatedViaTheNonce()
    {
        // The counter feeds the nonce, so changing it without re-encrypting makes the tag check fail exactly like tampering the ciphertext - can't silently roll a counter back.
        byte[] key = NewKey();
        byte[] wire = LoRaPrivatePayloadCrypto.Encrypt(key, NewBootNonce(), 5, "hello");
        BinaryPrimitives.WriteUInt32BigEndian(wire.AsSpan(9, 4), 1);

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_TooShort_ReturnsNull()
    {
        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), new byte[10]);

        Assert.Null(plaintext);
        Assert.Equal(0U, counter);
    }

    [Fact]
    public void Decrypt_WrongKeyLength_ReturnsNull()
    {
        byte[] wire = LoRaPrivatePayloadCrypto.Encrypt(NewKey(), NewBootNonce(), 1, "hello");

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(new byte[16], wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_EmptyPlaintext_StillRoundTrips()
    {
        byte[] key = NewKey();
        byte[] wire = LoRaPrivatePayloadCrypto.Encrypt(key, NewBootNonce(), 1, "");

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Equal("", plaintext);
        Assert.Equal(1U, counter);
    }

    [Fact]
    public void Decrypt_LegacyCounterShapedFrame_RejectedAsMalformed()
    {
        // The retired counter-based wire format's leading byte was always 0x00 (never a version tag) - no transition window, any node still sending this shape has not been reflashed.
        byte[] wire = { 0x00, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), wire);

        Assert.Null(plaintext);
        Assert.Null(bootNonce);
    }

    [Fact]
    public void Decrypt_MalformedLeadingByte_ReturnsNull()
    {
        // Not 0x02 - must be rejected outright, not misparsed.
        byte[] wire = { 0x01, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), wire);

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

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(masterKey, frame);

        Assert.Equal(vector.PlaintextUtf8, plaintext);
        Assert.Equal(vector.Counter, counter);
        Assert.Equal(expectedBootNonce, bootNonce);
    }

    [Fact]
    public void Decrypt_WrongMasterKey_ReturnsNull()
    {
        byte[] frame = LoRaPrivatePayloadCrypto.Encrypt(NewKey(), NewBootNonce(), 1, "{\"battery\":50}");

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), frame);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_TooShortForPrefixPlusTag_ReturnsNull()
    {
        byte[] wire = new byte[1 + 8 + 4 + 15]; // one byte short of the minimum (empty ciphertext + 16-byte tag)
        wire[0] = 0x02;

        (string? plaintext, uint counter, byte[]? bootNonce) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), wire);

        Assert.Null(plaintext);
    }
}
