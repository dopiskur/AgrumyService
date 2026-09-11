using System.Buffers.Binary;
using System.Text.Json;
using Agrumy.Api.Storage;
using Agrumy.Shared.Models;
using Microsoft.Data.Sqlite;

namespace Agrumy.Api.Arkod
{
    /// Reads a single parcel's geometry+attributes out of the local ARKOD GeoPackage mirror (ArkodGeoPackageStorage) by ARKOD/JPA id - the offline fallback for the primary WMS GetFeatureInfo click-lookup (parcel-geometry-map.js), same source data either way. GeoPackage's own binary blob header (magic+version+flags+srs_id+envelope) is parsed by hand - documented, confirmed against a real downloaded file during development - before handing the remaining bytes to a small WKB Polygon/MultiPolygon reader; vertices come out in EPSG:3765 metres and are reprojected via Htrs96TransverseMercator.
    public sealed class ArkodGeoPackageLookup(ArkodGeoPackageStorage storage)
    {
        public async Task<ArkodParcelLookupResult?> TryFindByJpaIdAsync(string jpaId, CancellationToken ct = default)
        {
            if (!storage.Exists || string.IsNullOrWhiteSpace(jpaId))
            {
                return null;
            }

            await using var connection = new SqliteConnection($"Data Source={storage.GeoPackagePath};Mode=ReadOnly");
            await connection.OpenAsync(ct);

            (string table, string geomColumn) = await ResolveGeometryTableAsync(connection, ct);
            if (table.Length == 0)
            {
                return null;
            }

            // Best-effort - the file has no index on jpaid as shipped; a first-ever lookup pays for a full scan while building this, every later one is fast. A read-only mount (or an export with no jpaid column) just means every lookup stays a full scan.
            try
            {
                await using var indexCmd = connection.CreateCommand();
                indexCmd.CommandText = $"CREATE INDEX IF NOT EXISTS idx_arkod_jpaid ON \"{table}\"(jpaid)";
                await indexCmd.ExecuteNonQueryAsync(ct);
            }
            catch (SqliteException)
            {
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT home_name, area, \"{geomColumn}\" FROM \"{table}\" WHERE jpaid = @jpaid LIMIT 1";
            command.Parameters.AddWithValue("@jpaid", jpaId);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return null;
            }

            string? homeName = reader.IsDBNull(0) ? null : reader.GetString(0);
            double? areaM2 = reader.IsDBNull(1) ? null : reader.GetDouble(1);
            var blob = (byte[])reader[2];

            string? geoJson = ParseAndReproject(blob);
            return geoJson == null ? null : new ArkodParcelLookupResult { ArkodParcelId = jpaId, HomeName = homeName, AreaM2 = areaM2, GeometryGeoJson = geoJson };
        }

        private static async Task<(string Table, string GeomColumn)> ResolveGeometryTableAsync(SqliteConnection connection, CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT table_name, column_name FROM gpkg_geometry_columns LIMIT 1";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? (reader.GetString(0), reader.GetString(1)) : ("", "");
        }

        /// GeoPackage binary header (OGC 12-128r19 §2.1.3) -> WKB Polygon/MultiPolygon -> WGS84 GeoJSON Polygon. A MultiPolygon export degrades to its first part rather than failing the whole lookup - every parcel sampled during development (5000 rows) was a plain Polygon.
        private static string? ParseAndReproject(byte[] blob)
        {
            if (blob.Length < 8 || blob[0] != (byte)'G' || blob[1] != (byte)'P')
            {
                return null;
            }
            byte flags = blob[3];
            int envelopeCode = (flags >> 1) & 0x7;
            int envelopeSize = envelopeCode switch { 1 => 32, 2 => 48, 3 => 48, 4 => 64, _ => 0 };
            int headerLen = 8 + envelopeSize;
            if (blob.Length <= headerLen)
            {
                return null;
            }

            List<List<(double X, double Y)>> rings = ReadWkbPolygonRings(blob, headerLen);
            if (rings.Count == 0)
            {
                return null;
            }

            double[][][] coordinates = rings
                .Select(ring => ring.Select(p =>
                {
                    (double lon, double lat) = Htrs96TransverseMercator.ToWgs84(p.X, p.Y);
                    return new[] { lon, lat };
                }).ToArray())
                .ToArray();
            return JsonSerializer.Serialize(new { type = "Polygon", coordinates });
        }

        private static List<List<(double X, double Y)>> ReadWkbPolygonRings(byte[] blob, int offset)
        {
            int pos = offset;
            bool littleEndian = blob[pos] == 1;
            pos += 1;
            uint geomType = ReadUInt32(blob, ref pos, littleEndian);

            if (geomType == 6) // MultiPolygon - take its first Polygon only, see ParseAndReproject's doc comment.
            {
                uint numPolygons = ReadUInt32(blob, ref pos, littleEndian);
                if (numPolygons == 0)
                {
                    return [];
                }
                bool innerLittleEndian = blob[pos] == 1;
                pos += 1;
                uint innerType = ReadUInt32(blob, ref pos, innerLittleEndian);
                return innerType == 3 ? ReadPolygonRings(blob, ref pos, innerLittleEndian) : [];
            }
            return geomType == 3 ? ReadPolygonRings(blob, ref pos, littleEndian) : [];
        }

        private static List<List<(double X, double Y)>> ReadPolygonRings(byte[] blob, ref int pos, bool littleEndian)
        {
            var rings = new List<List<(double X, double Y)>>();
            uint numRings = ReadUInt32(blob, ref pos, littleEndian);
            for (int r = 0; r < numRings; r++)
            {
                uint numPoints = ReadUInt32(blob, ref pos, littleEndian);
                var ring = new List<(double X, double Y)>((int)numPoints);
                for (int p = 0; p < numPoints; p++)
                {
                    double x = ReadDouble(blob, ref pos, littleEndian);
                    double y = ReadDouble(blob, ref pos, littleEndian);
                    ring.Add((x, y));
                }
                rings.Add(ring);
            }
            return rings;
        }

        private static uint ReadUInt32(byte[] blob, ref int pos, bool littleEndian)
        {
            uint value = littleEndian
                ? BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos, 4))
                : BinaryPrimitives.ReadUInt32BigEndian(blob.AsSpan(pos, 4));
            pos += 4;
            return value;
        }

        private static double ReadDouble(byte[] blob, ref int pos, bool littleEndian)
        {
            long bits = littleEndian
                ? BinaryPrimitives.ReadInt64LittleEndian(blob.AsSpan(pos, 8))
                : BinaryPrimitives.ReadInt64BigEndian(blob.AsSpan(pos, 8));
            pos += 8;
            return BitConverter.Int64BitsToDouble(bits);
        }
    }
}
