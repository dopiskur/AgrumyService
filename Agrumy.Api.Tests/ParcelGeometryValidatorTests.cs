using Agrumy.Shared.Geo;
using Xunit;

namespace Agrumy.Api.Tests;

public class ParcelGeometryValidatorTests
{
    // Roughly a 100m x 100m square near Zagreb - about 1 ha.
    private const string SquareKmGeoJson = """{"type":"Polygon","coordinates":[[[15.9,45.8],[15.9012,45.8],[15.9012,45.8009],[15.9,45.8009],[15.9,45.8]]]}""";

    [Fact]
    public void Validate_ValidSquarePolygon_ReturnsPositiveAreaAndMatchingBbox()
    {
        ParcelGeometryResult result = ParcelGeometryValidator.Validate(SquareKmGeoJson);

        Assert.True(result.AreaHectares is > 0.5 and < 1.5, $"Expected roughly 1 ha, got {result.AreaHectares}");
        Assert.Equal(45.8, result.BboxMinLat, precision: 6);
        Assert.Equal(15.9, result.BboxMinLon, precision: 6);
        Assert.Equal(45.8009, result.BboxMaxLat, precision: 6);
        Assert.Equal(15.9012, result.BboxMaxLon, precision: 6);
    }

    [Fact]
    public void Validate_NotJson_Throws() =>
        Assert.Throws<ArgumentException>(() => ParcelGeometryValidator.Validate("not json at all"));

    [Fact]
    public void Validate_WrongGeometryType_Throws()
    {
        const string point = """{"type":"Point","coordinates":[15.9,45.8]}""";
        Assert.Throws<ArgumentException>(() => ParcelGeometryValidator.Validate(point));
    }

    [Fact]
    public void Validate_UnclosedRing_Throws()
    {
        const string unclosed = """{"type":"Polygon","coordinates":[[[15.9,45.8],[15.91,45.8],[15.91,45.81],[15.9,45.81]]]}""";
        Assert.Throws<ArgumentException>(() => ParcelGeometryValidator.Validate(unclosed));
    }

    [Fact]
    public void Validate_TooFewPoints_Throws()
    {
        const string triangle = """{"type":"Polygon","coordinates":[[[15.9,45.8],[15.91,45.8],[15.9,45.8]]]}""";
        Assert.Throws<ArgumentException>(() => ParcelGeometryValidator.Validate(triangle));
    }

    [Fact]
    public void Validate_CoordinateOutOfRange_Throws()
    {
        const string badLat = """{"type":"Polygon","coordinates":[[[15.9,95.0],[15.91,45.8],[15.91,45.81],[15.9,45.81],[15.9,95.0]]]}""";
        Assert.Throws<ArgumentException>(() => ParcelGeometryValidator.Validate(badLat));
    }

    [Fact]
    public void Validate_TooManyVertices_Throws()
    {
        var points = new List<double[]>();
        for (int i = 0; i < ParcelGeometryValidator.MaxVertices + 5; i++)
        {
            double angle = 2 * Math.PI * i / (ParcelGeometryValidator.MaxVertices + 5);
            points.Add(new[] { 15.9 + (0.01 * Math.Cos(angle)), 45.8 + (0.01 * Math.Sin(angle)) });
        }
        points.Add(points[0]);
        string geoJson = System.Text.Json.JsonSerializer.Serialize(new { type = "Polygon", coordinates = new[] { points } });
        Assert.Throws<ArgumentException>(() => ParcelGeometryValidator.Validate(geoJson));
    }

    [Fact]
    public void Validate_SelfIntersectingBowtie_Throws()
    {
        // Bowtie: (0,0) -> (1,1) -> (1,0) -> (0,1) -> (0,0), the two diagonals cross.
        const string bowtie = """{"type":"Polygon","coordinates":[[[15.9,45.8],[15.91,45.81],[15.91,45.8],[15.9,45.81],[15.9,45.8]]]}""";
        Assert.Throws<ArgumentException>(() => ParcelGeometryValidator.Validate(bowtie));
    }

    [Fact]
    public void Validate_ZeroAreaDegenerateLine_Throws()
    {
        const string degenerate = """{"type":"Polygon","coordinates":[[[15.9,45.8],[15.91,45.8],[15.9,45.8],[15.91,45.8],[15.9,45.8]]]}""";
        Assert.Throws<ArgumentException>(() => ParcelGeometryValidator.Validate(degenerate));
    }
}
