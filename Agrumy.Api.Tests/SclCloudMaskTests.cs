using Agrumy.Api.Satellite;
using Xunit;

namespace Agrumy.Api.Tests;

public class SclCloudMaskTests
{
    [Theory]
    [InlineData(0, false)]  // NO_DATA
    [InlineData(1, false)]  // SATURATED_DEFECTIVE
    [InlineData(2, true)]   // DARK_AREA_PIXELS
    [InlineData(3, false)]  // CLOUD_SHADOWS
    [InlineData(4, true)]   // VEGETATION
    [InlineData(5, true)]   // NOT_VEGETATED
    [InlineData(6, true)]   // WATER
    [InlineData(7, false)]  // UNCLASSIFIED
    [InlineData(8, false)]  // CLOUD_MEDIUM_PROBABILITY
    [InlineData(9, false)]  // CLOUD_HIGH_PROBABILITY
    [InlineData(10, false)] // THIN_CIRRUS
    [InlineData(11, true)]  // SNOW_ICE
    public void IsValid_MatchesTheDocumentedScl3Classification(int sclClass, bool expectedValid) =>
        Assert.Equal(expectedValid, SclCloudMask.IsValid(sclClass));

    [Fact]
    public void ValidPixelPercent_ComputesFromAKnownPixelVector()
    {
        // 10 pixels: 6 valid (VEGETATION), 4 invalid (2x cloud-high, 2x cloud-shadow) -> 60%.
        int[] pixels = [4, 4, 4, 4, 4, 4, 9, 9, 3, 3];

        double percent = SclCloudMask.ValidPixelPercent(pixels);

        Assert.Equal(60.0, percent);
    }

    [Fact]
    public void ValidPixelPercent_AllCloud_ReturnsZero() =>
        Assert.Equal(0.0, SclCloudMask.ValidPixelPercent([8, 8, 9, 9, 3]));

    [Fact]
    public void ValidPixelPercent_EmptyVector_ReturnsZero_NotDivideByZero() =>
        Assert.Equal(0.0, SclCloudMask.ValidPixelPercent([]));
}
