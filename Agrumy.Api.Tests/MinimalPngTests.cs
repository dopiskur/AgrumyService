using Agrumy.Api.Satellite;
using Xunit;

namespace Agrumy.Api.Tests;

public class MinimalPngTests
{
    [Fact]
    public void Grayscale8_RoundTrips_ExactPixelValues()
    {
        const int width = 5, height = 3;
        var pixels = new byte[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i * 17 % 256);
        }

        byte[] png = MinimalPng.EncodeGrayscale8(width, height, pixels);
        MinimalPng.Decoded decoded = MinimalPng.Decode(png);

        Assert.Equal(width, decoded.Width);
        Assert.Equal(height, decoded.Height);
        Assert.Equal(1, decoded.BytesPerPixel);
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void Rgb8_RoundTrips_ExactPixelValues()
    {
        const int width = 4, height = 4;
        var pixels = new byte[width * height * 3];
        var rnd = new Random(42);
        rnd.NextBytes(pixels);

        byte[] png = MinimalPng.EncodeRgb8(width, height, pixels);
        MinimalPng.Decoded decoded = MinimalPng.Decode(png);

        Assert.Equal(3, decoded.BytesPerPixel);
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void Rgba8_RoundTrips_ExactPixelValues()
    {
        const int width = 6, height = 2;
        var pixels = new byte[width * height * 4];
        var rnd = new Random(7);
        rnd.NextBytes(pixels);

        byte[] png = MinimalPng.EncodeRgba8(width, height, pixels);
        MinimalPng.Decoded decoded = MinimalPng.Decode(png);

        Assert.Equal(4, decoded.BytesPerPixel);
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void Decode_RejectsNonPngBytes()
    {
        Assert.Throws<InvalidDataException>(() => MinimalPng.Decode([1, 2, 3, 4]));
    }

    [Fact]
    public void SinglePixelImages_RoundTrip()
    {
        byte[] png = MinimalPng.EncodeGrayscale8(1, 1, [200]);
        MinimalPng.Decoded decoded = MinimalPng.Decode(png);
        Assert.Equal([(byte)200], decoded.Pixels);
    }

    [Fact]
    public void AllFilterableGradients_RoundTrip()
    {
        // A smooth gradient exercises the Sub/Up/Average/Paeth predictors differently than random noise -
        // DeflateStream's own filter selection (implicit at filter-type 0 here) doesn't vary, but this still
        // catches an off-by-one in scanline stride/indexing that flat random data might accidentally hide.
        const int width = 32, height = 32;
        var pixels = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = (byte)((x * 8) + y);
            }
        }

        byte[] png = MinimalPng.EncodeGrayscale8(width, height, pixels);
        MinimalPng.Decoded decoded = MinimalPng.Decode(png);

        Assert.Equal(pixels, decoded.Pixels);
    }
}
