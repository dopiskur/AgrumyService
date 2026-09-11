using System.Text.Json;

namespace Agrumy.Shared.Geo
{
    public sealed record ParcelGeometryResult(string GeometryGeoJson, double AreaHectares, double BboxMinLat, double BboxMinLon, double BboxMaxLat, double BboxMaxLon);

    /// Validates a WGS84 GeoJSON Polygon for parcel/zone boundaries (Detaljni dizajn S-A step 2) - closed ring, vertex cap, in-range coordinates, no self-intersection, positive area.
    public static class ParcelGeometryValidator
    {
        public const int MaxVertices = 500;

        public static ParcelGeometryResult Validate(string geoJson)
        {
            if (string.IsNullOrWhiteSpace(geoJson))
            {
                throw new ArgumentException("Geometry is required.");
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(geoJson);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid GeoJSON.", ex);
            }

            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("type", out JsonElement typeEl) || typeEl.GetString() != "Polygon")
            {
                throw new ArgumentException("Geometry must be a GeoJSON Polygon.");
            }
            if (!root.TryGetProperty("coordinates", out JsonElement coordsEl) || coordsEl.ValueKind != JsonValueKind.Array || coordsEl.GetArrayLength() == 0)
            {
                throw new ArgumentException("Polygon has no coordinates.");
            }

            var points = new List<(double Lon, double Lat)>();
            foreach (JsonElement pt in coordsEl[0].EnumerateArray())
            {
                if (pt.ValueKind != JsonValueKind.Array || pt.GetArrayLength() < 2)
                {
                    throw new ArgumentException("Each coordinate must be a [lon, lat] pair.");
                }
                double lon = pt[0].GetDouble();
                double lat = pt[1].GetDouble();
                if (lat is < -90 or > 90 || lon is < -180 or > 180)
                {
                    throw new ArgumentException("Coordinate out of range.");
                }
                points.Add((lon, lat));
            }

            if (points.Count < 4)
            {
                throw new ArgumentException("Polygon ring needs at least 4 points (closed).");
            }
            if (points.Count > MaxVertices)
            {
                throw new ArgumentException($"Polygon exceeds {MaxVertices} vertices.");
            }
            (double Lon, double Lat) first = points[0];
            (double Lon, double Lat) last = points[^1];
            if (Math.Abs(first.Lon - last.Lon) > 1e-9 || Math.Abs(first.Lat - last.Lat) > 1e-9)
            {
                throw new ArgumentException("Polygon ring must be closed (first point == last point).");
            }
            if (HasSelfIntersection(points))
            {
                throw new ArgumentException("Polygon must not self-intersect.");
            }

            double areaHa = SphericalPolygonAreaHectares(points);
            if (areaHa <= 0)
            {
                throw new ArgumentException("Polygon area must be greater than zero.");
            }

            return new ParcelGeometryResult(
                geoJson,
                areaHa,
                points.Min(p => p.Lat),
                points.Min(p => p.Lon),
                points.Max(p => p.Lat),
                points.Max(p => p.Lon));
        }

        /// Equirectangular projection centered on the ring's own latitude then shoelace - accurate to well under 0.1% at parcel scale (a few hectares), no GIS library needed.
        private static double SphericalPolygonAreaHectares(List<(double Lon, double Lat)> points)
        {
            double avgLatRad = points.Average(p => p.Lat) * Math.PI / 180.0;
            double metersPerDegLat = 111320.0;
            double metersPerDegLon = 111320.0 * Math.Cos(avgLatRad);
            double area = 0;
            for (int i = 0; i < points.Count - 1; i++)
            {
                (double Lon, double Lat) a = points[i];
                (double Lon, double Lat) b = points[i + 1];
                double x1 = a.Lon * metersPerDegLon, y1 = a.Lat * metersPerDegLat;
                double x2 = b.Lon * metersPerDegLon, y2 = b.Lat * metersPerDegLat;
                area += (x1 * y2) - (x2 * y1);
            }
            return Math.Abs(area) / 2.0 / 10000.0;
        }

        private static bool HasSelfIntersection(List<(double Lon, double Lat)> points)
        {
            int n = points.Count - 1;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    bool adjacent = j == i + 1 || (i == 0 && j == n - 1);
                    if (adjacent)
                    {
                        continue;
                    }
                    if (SegmentsIntersect(points[i], points[i + 1], points[j], points[j + 1]))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool SegmentsIntersect((double Lon, double Lat) p1, (double Lon, double Lat) p2, (double Lon, double Lat) p3, (double Lon, double Lat) p4)
        {
            double d1 = Cross(p3, p4, p1);
            double d2 = Cross(p3, p4, p2);
            double d3 = Cross(p1, p2, p3);
            double d4 = Cross(p1, p2, p4);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double Cross((double Lon, double Lat) a, (double Lon, double Lat) b, (double Lon, double Lat) c) =>
            ((b.Lon - a.Lon) * (c.Lat - a.Lat)) - ((b.Lat - a.Lat) * (c.Lon - a.Lon));
    }
}
