using Agrumy.Api.Satellite;
using Agrumy.Shared.Models;
using Xunit;

namespace Agrumy.Api.Tests;

public class SatellitePaletteRendererTests
{
    private static byte[] BuildGrid(int width, int height, byte[] pixels)
    {
        var grid = new byte[8 + pixels.Length];
        BitConverter.GetBytes(width).CopyTo(grid, 0);
        BitConverter.GetBytes(height).CopyTo(grid, 4);
        pixels.CopyTo(grid, 8);
        return grid;
    }

    [Fact]
    public void Decode_ReadsBackTheWidthHeightHeaderAndPixels()
    {
        byte[] grid = BuildGrid(3, 2, [10, 20, 30, 40, 50, 60]);

        SatellitePaletteRenderer.DecodedGrid decoded = SatellitePaletteRenderer.Decode(grid);

        Assert.Equal(3, decoded.Width);
        Assert.Equal(2, decoded.Height);
        Assert.Equal(new byte[] { 10, 20, 30, 40, 50, 60 }, decoded.Pixels);
    }

    [Fact]
    public void Decode_MismatchedPayloadLength_Throws()
    {
        byte[] grid = BuildGrid(3, 2, [1, 2, 3]); // should be 6 pixels for 3x2, only 3 given
        Assert.Throws<ArgumentException>(() => SatellitePaletteRenderer.Decode(grid));
    }

    [Fact]
    public void RenderPng_InvalidPixel_IsFullyTransparent()
    {
        byte[] grid = BuildGrid(1, 1, [0xFF]);

        byte[] png = SatellitePaletteRenderer.RenderPng(grid, SatelliteIndex.Ndvi);
        MinimalPng.Decoded decoded = MinimalPng.Decode(png);

        Assert.Equal(4, decoded.BytesPerPixel);
        Assert.Equal(0, decoded.Pixels[3]); // alpha channel
    }

    [Fact]
    public void RenderPng_ValidPixel_IsOpaqueAndUsesThePalette()
    {
        byte[] grid = BuildGrid(1, 1, [128]);

        byte[] png = SatellitePaletteRenderer.RenderPng(grid, SatelliteIndex.Ndvi);
        MinimalPng.Decoded decoded = MinimalPng.Decode(png);

        Assert.Equal(255, decoded.Pixels[3]); // fully opaque
    }

    [Theory]
    [InlineData(SatelliteIndex.Ndvi)]
    [InlineData(SatelliteIndex.Ndmi)]
    [InlineData(SatelliteIndex.Ndwi)]
    [InlineData(SatelliteIndex.Ndsi)]
    public void RenderPng_EveryScalarIndex_ProducesADecodablePng(SatelliteIndex index)
    {
        var pixels = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            pixels[i] = (byte)i;
        }
        byte[] grid = BuildGrid(16, 16, pixels);

        byte[] png = SatellitePaletteRenderer.RenderPng(grid, index);
        MinimalPng.Decoded decoded = MinimalPng.Decode(png);

        Assert.Equal(16, decoded.Width);
        Assert.Equal(16, decoded.Height);
    }

    [Fact]
    public void RenderPng_CompositeIndex_ThrowsSinceItHasNoScalarPalette()
    {
        byte[] grid = BuildGrid(1, 1, [128]);
        Assert.Throws<ArgumentException>(() => SatellitePaletteRenderer.RenderPng(grid, SatelliteIndex.SwirComposite));
    }
}
