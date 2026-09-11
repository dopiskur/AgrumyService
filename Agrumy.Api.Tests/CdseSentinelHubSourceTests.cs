using System.Text.Json.Nodes;
using Agrumy.Api.Satellite;
using Xunit;

namespace Agrumy.Api.Tests;

/// ComputePixelDimensions is CdseSentinelHubSource's fix for CDSE's Process API silently rendering a 1x1 image when given resx/resy against a CRS84 (geographic) geometry - live-verified against real CDSE responses, this covers the pure conversion math on its own.
public class CdseSentinelHubSourceTests
{
    // Built via JsonNode, not string interpolation - a culture with a comma decimal separator would otherwise turn e.g. 0.01 into invalid JSON.
    private static string SquarePolygon(double minLon, double minLat, double maxLon, double maxLat)
    {
        var ring = new JsonArray(
            new JsonArray(minLon, minLat), new JsonArray(maxLon, minLat),
            new JsonArray(maxLon, maxLat), new JsonArray(minLon, maxLat), new JsonArray(minLon, minLat));
        var polygon = new JsonObject { ["type"] = "Polygon", ["coordinates"] = new JsonArray(ring) };
        return polygon.ToJsonString();
    }

    [Fact]
    public void NearEquator_EqualDegreeExtents_ProduceRoughlyEqualPixelDimensions()
    {
        string polygon = SquarePolygon(0.0, 0.0, 0.01, 0.01);

        (int width, int height) = CdseSentinelHubSource.ComputePixelDimensions(polygon, 10, 10);

        Assert.InRange(width, 108, 114);
        Assert.InRange(height, 108, 114);
    }

    [Fact]
    public void HighLatitude_LongitudeDegreesAreShorterInMeters_WidthShrinksRelativeToHeight()
    {
        // At 60 degrees latitude, a degree of longitude is about half the meters of a degree of latitude (cos(60)=0.5).
        string polygon = SquarePolygon(0.0, 60.0, 0.01, 60.01);

        (int width, int height) = CdseSentinelHubSource.ComputePixelDimensions(polygon, 10, 10);

        Assert.InRange(width, (int)(height * 0.4), (int)(height * 0.6));
    }

    [Fact]
    public void HugePolygonAtFineResolution_ClampsToMaxRenderPixels()
    {
        string polygon = SquarePolygon(0.0, 0.0, 10.0, 10.0);

        (int width, int height) = CdseSentinelHubSource.ComputePixelDimensions(polygon, 1, 1);

        Assert.Equal(2500, width);
        Assert.Equal(2500, height);
    }

    [Fact]
    public void DegeneratePolygon_ClampsToAtLeastOnePixel()
    {
        string polygon = SquarePolygon(15.98, 45.79, 15.980001, 45.790001);

        (int width, int height) = CdseSentinelHubSource.ComputePixelDimensions(polygon, 10, 10);

        Assert.True(width >= 1);
        Assert.True(height >= 1);
    }
}
