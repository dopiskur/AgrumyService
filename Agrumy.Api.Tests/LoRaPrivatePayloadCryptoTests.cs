using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using api.LoRa;

namespace Agrumy.Api.Tests;

/// Covers api.LoRa.LoRaPrivatePayloadCrypto.Decrypt against ciphertext built the same way AgrumyFirmware's
/// LoRaPrivateController will (AES-256-GCM, nonce = 4 zero bytes + 8-byte big-endian counter) - the actual
/// over-the-air round trip is untestable without hardware, same status as MqttCommandPublisherTests' own network call.
public class LoRaPrivatePayloadCryptoTests
{
    private static byte[] NewKey()
    {
        byte[] key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// Mirrors the wire format Decrypt expects - not production code (only the firmware ever encrypts), just this test's own fixture builder.
    private static byte[] Encrypt(byte[] key, ulong counter, string plaintext)
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
        byte[] wire = Encrypt(key, 42, "{\"t\":\"sensor\",\"d\":[]}");

        (string? plaintext, ulong counter) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Equal("{\"t\":\"sensor\",\"d\":[]}", plaintext);
        Assert.Equal(42UL, counter);
    }

    [Fact]
    public void Decrypt_WrongKey_ReturnsNull()
    {
        byte[] wire = Encrypt(NewKey(), 1, "hello");

        (string? plaintext, ulong counter) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), wire);

        Assert.Null(plaintext);
        Assert.Equal(0UL, counter);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ReturnsNull_TagCheckFails()
    {
        byte[] key = NewKey();
        byte[] wire = Encrypt(key, 1, "hello");
        wire[10] ^= 0xFF; // flip a byte inside the ciphertext region

        (string? plaintext, ulong counter) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_TamperedCounter_ReturnsNull_BecauseItsAuthenticatedViaTheNonce()
    {
        // The counter feeds the nonce, so changing it without re-encrypting makes the tag check fail exactly like tampering the ciphertext - can't silently roll a counter back.
        byte[] key = NewKey();
        byte[] wire = Encrypt(key, 5, "hello");
        BinaryPrimitives.WriteUInt64BigEndian(wire.AsSpan(0, 8), 1);

        (string? plaintext, ulong counter) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_TooShort_ReturnsNull()
    {
        (string? plaintext, ulong counter) = LoRaPrivatePayloadCrypto.Decrypt(NewKey(), new byte[10]);

        Assert.Null(plaintext);
        Assert.Equal(0UL, counter);
    }

    [Fact]
    public void Decrypt_WrongKeyLength_ReturnsNull()
    {
        byte[] wire = Encrypt(NewKey(), 1, "hello");

        (string? plaintext, ulong counter) = LoRaPrivatePayloadCrypto.Decrypt(new byte[16], wire);

        Assert.Null(plaintext);
    }

    [Fact]
    public void Decrypt_EmptyPlaintext_StillRoundTrips()
    {
        byte[] key = NewKey();
        byte[] wire = Encrypt(key, 1, "");

        (string? plaintext, ulong counter) = LoRaPrivatePayloadCrypto.Decrypt(key, wire);

        Assert.Equal("", plaintext);
        Assert.Equal(1UL, counter);
    }
}
