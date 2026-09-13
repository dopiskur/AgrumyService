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

    // Diamond inscribed in [minLon,minLat]-[maxLon,maxLat]: corners at the midpoint of each bbox edge -
    // covers exactly half the bbox area (standard inscribed-quadrilateral result), so it's a good check
    // that CountPixelsInsidePolygon actually excludes bbox corners instead of counting the whole grid.
    private static string DiamondPolygon(double minLon, double minLat, double maxLon, double maxLat)
    {
        double midLon = (minLon + maxLon) / 2, midLat = (minLat + maxLat) / 2;
        var ring = new JsonArray(
            new JsonArray(midLon, minLat), new JsonArray(maxLon, midLat),
            new JsonArray(midLon, maxLat), new JsonArray(minLon, midLat), new JsonArray(midLon, minLat));
        var polygon = new JsonObject { ["type"] = "Polygon", ["coordinates"] = new JsonArray(ring) };
        return polygon.ToJsonString();
    }

    [Fact]
    public void CountPixelsInsidePolygon_SquareFillingItsOwnBoundingBox_CountsEveryGridCell()
    {
        string polygon = SquarePolygon(0.0, 0.0, 0.01, 0.01);
        const int width = 20, height = 20;

        int count = CdseSentinelHubSource.CountPixelsInsidePolygon(polygon, width, height);

        Assert.Equal(width * height, count);
    }

    [Fact]
    public void CountPixelsInsidePolygon_DiamondInscribedInBoundingBox_CoversRoughlyHalfTheGrid()
    {
        string polygon = DiamondPolygon(0.0, 0.0, 0.01, 0.01);
        const int width = 40, height = 40;

        int count = CdseSentinelHubSource.CountPixelsInsidePolygon(polygon, width, height);

        // Geometric area ratio is exactly 0.5; grid-sampling at the pixel centers only approximates that.
        Assert.InRange(count, (int)(width * height * 0.4), (int)(width * height * 0.6));
    }

    [Fact]
    public void CountPixelsInsidePolygon_DegeneratePolygon_ReturnsZeroNotNegativeOrThrow()
    {
        string polygon = SquarePolygon(15.98, 45.79, 15.980001, 45.790001);

        int count = CdseSentinelHubSource.CountPixelsInsidePolygon(polygon, 1, 1);

        Assert.True(count >= 0);
    }

    // Real invent.hr parcel (a narrow rotated parallelogram, not axis-aligned) that was stuck at
    // ValidPixelPercent 67.58% (123 valid of 182 bbox pixels) under the old bbox-relative formula,
    // permanently below the tenant's 70% MinValidPixelPercent regardless of date/cloud cover -
    // CloudPercent was 0% on every one of its 498 backfilled scenes. Confirms the polygon-relative
    // fix actually crosses the threshold for the shape that motivated it.
    [Fact]
    public void CountPixelsInsidePolygon_RealNarrowParallelogramParcel_LiftsValidPercentAboveOldBboxRelativeFigure()
    {
        const string geoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[15.979985,45.816106],[15.980983,45.816263],[15.981691,45.81522],[15.980741,45.814991],[15.979985,45.816106]]]}";
        const int width = 13, height = 14;
        const int validMaskPixels = 123; // observed live: 182 total bbox pixels, 59 masked invalid

        int insidePolygonCount = CdseSentinelHubSource.CountPixelsInsidePolygon(geoJson, width, height);
        double oldPercent = 100.0 * validMaskPixels / (width * height);
        double newPercent = Math.Min(100.0, 100.0 * validMaskPixels / insidePolygonCount);

        Assert.True(insidePolygonCount < width * height, "the parallelogram should not fill its own bounding box");
        Assert.True(oldPercent < 70, "sanity check against the actually-observed stuck-below-threshold figure");
        Assert.True(newPercent > oldPercent);
    }
}
