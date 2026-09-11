namespace Agrumy.Api.Arkod
{
    /// Inverse Transverse Mercator for EPSG:3765 (HTRS96 / Croatia TM) -> WGS84 lon/lat, the CRS ARKOD's GeoPackage export ships in (confirmed via gpkg_geometry_columns on a real downloaded file). No general-purpose reprojection library dependency for one fixed, well-known projection - the standard Snyder/Krüger series, verified against pyproj for a real ARKOD vertex to within 1e-6 degrees during development.
    public static class Htrs96TransverseMercator
    {
        private const double A = 6378137.0; // GRS80 semi-major axis (metres)
        private const double InvF = 298.257222101; // GRS80 inverse flattening
        private const double K0 = 0.9999;
        private static readonly double Lon0 = double.DegreesToRadians(16.5);
        private const double FalseEasting = 500000.0;
        private const double FalseNorthing = 0.0;

        /// x/y in EPSG:3765 metres -> (Lon, Lat) in WGS84 degrees, GeoJSON coordinate order.
        public static (double Lon, double Lat) ToWgs84(double x, double y)
        {
            double f = 1 / InvF;
            double e2 = f * (2 - f);
            x -= FalseEasting;
            y -= FalseNorthing;

            double m = y / K0;
            double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));
            double mu = m / (A * (1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256));
            double phi1 = mu
                + (3 * e1 / 2 - 27 * Math.Pow(e1, 3) / 32) * Math.Sin(2 * mu)
                + (21 * e1 * e1 / 16 - 55 * Math.Pow(e1, 4) / 32) * Math.Sin(4 * mu)
                + (151 * Math.Pow(e1, 3) / 96) * Math.Sin(6 * mu);

            double ep2 = e2 / (1 - e2);
            double c1 = ep2 * Math.Cos(phi1) * Math.Cos(phi1);
            double t1 = Math.Tan(phi1) * Math.Tan(phi1);
            double n1 = A / Math.Sqrt(1 - e2 * Math.Sin(phi1) * Math.Sin(phi1));
            double r1 = A * (1 - e2) / Math.Pow(1 - e2 * Math.Sin(phi1) * Math.Sin(phi1), 1.5);
            double d = x / (n1 * K0);

            double lat = phi1 - (n1 * Math.Tan(phi1) / r1) *
                (d * d / 2
                 - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * ep2) * Math.Pow(d, 4) / 24
                 + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * ep2 - 3 * c1 * c1) * Math.Pow(d, 6) / 720);
            double lon = Lon0 + (d
                - (1 + 2 * t1 + c1) * Math.Pow(d, 3) / 6
                + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * ep2 + 24 * t1 * t1) * Math.Pow(d, 5) / 120) / Math.Cos(phi1);

            return (double.RadiansToDegrees(lon), double.RadiansToDegrees(lat));
        }
    }
}
