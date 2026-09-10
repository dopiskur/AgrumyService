using Agrumy.Dal;
using System.Globalization;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Agrumy.Api.Dal
{
    /// One 5-minute bucket's SQL-side aggregate, used only by OptimizeOldSensorDataAsync's retention downsampling - TenantID/DeviceFarmUnitID/DeviceFarmUnitZoneID come from MAX() (a device's own FK columns rarely change; MAX just needs to pick one consistent value per bucket, not the literal most-recent row).
    internal sealed class OptimizedSensorBucket
    {
        public int? TenantID { get; set; }
        public int? DeviceFarmUnitID { get; set; }
        public int? DeviceFarmUnitZoneID { get; set; }
        public double? Battery { get; set; }
        public double? Temperature { get; set; }
        public double? SoilTemperature { get; set; }
        public double? Humidity { get; set; }
        public double? Moisture { get; set; }
        public double? Light { get; set; }
        public double? Co2 { get; set; }
        public double? Tvoc { get; set; }
        public double? Barometer { get; set; }
        public double? LiquidPH { get; set; }
        public double? RainLevel { get; set; }
        public double? WaterLevel { get; set; }
        public double? Wind { get; set; }
        public double? Ec { get; set; }
        public double? Weight { get; set; }
        public DateTime BucketStart { get; set; }
    }

    /// ISensorDataRepository, extracted out of the EfRepository god class - a leaf facet, its only cross-facet dependency being the experiment dual-write below.
    internal sealed class EfSensorDataRepository(AgrumyDbContext db, IExperimentRepository experimentRepository) : ISensorDataRepository
    {
        public async Task SensorDataPushAsync(IReadOnlyList<SensorDataPushReading> readings, int deviceID, int tenantID, int? deviceFarmUnitID, int? deviceFarmUnitZoneID)
        {
            if (readings.Count == 0)
            {
                return;
            }

            // Mirrors this same push into dataSensorExperiment when the zone is currently under an active experiment, alongside (never instead of) the normal insert below.
            if (deviceFarmUnitZoneID is int idZone && await experimentRepository.ActiveExperimentIdForZoneAsync(idZone) is int idExperiment)
            {
                await experimentRepository.SensorDataExperimentAddRangeAsync(idExperiment, deviceID, tenantID, readings);
            }

            var rows = readings.Select(r => new SensorDataRow
            {
                // Identity is server-authoritative - the matching fields on each reading are deliberately ignored.
                DeviceID = deviceID,
                TenantID = tenantID,
                DeviceFarmUnitID = deviceFarmUnitID,
                DeviceFarmUnitZoneID = deviceFarmUnitZoneID,
                Battery = r.Battery,
                Temperature = r.Temperature,
                SoilTemperature = r.SoilTemperature,
                Humidity = r.Humidity,
                Moisture = r.Moisture,
                Light = r.Light,
                Co2 = r.Co2,
                Tvoc = r.Tvoc,
                Barometer = r.Barometer,
                LiquidPH = r.LiquidPH,
                RainLevel = r.RainLevel,
                WaterLevel = r.WaterLevel,
                Wind = r.Wind,
                Ec = r.Ec,
                Weight = r.Weight,
                WifiRssiDbm = r.WifiRssiDbm,
                LoRaRssiDbm = r.LoRaRssiDbm,
                LoRaSnrDb = r.LoRaSnrDb,
                // A missing/unparseable/blank timestamp becomes "now" (UTC - device timestamps are UTC).
                DateCreated = ReadDateTime(r.DateCreated) ?? DateTime.UtcNow,
            });

            db.SensorData.AddRange(rows);
            await db.SaveChangesAsync();
        }

        // Both the app's own window cap and the hardcoded provider-side units (BucketExpr only knows minute/hour/day/the fixed 5-minute retention bucket) - a from/to spanning more than this would load an unreasonably large bucketed result set even with SQL-side aggregation.
        private const int MaxLookbackDays = 400;

        private static bool IsValidWindow(DateTimeOffset from, DateTimeOffset to) =>
            to > from && (to - from) <= TimeSpan.FromDays(MaxLookbackDays);

        /// date_trunc (Postgres, no TimescaleDB dependency - plain date_trunc covers minute/hour/day without needing time_bucket/hypertables) / DATE_FORMAT truncation (MySQL/MariaDB) for the column named literally in the SQL this builds - bucket is a closed 3-value enum, never user text, so splicing its mapped fragment directly into the query string carries no injection risk.
        private static string BucketExpr(SensorDataBucket bucket, bool isNpgsql, string column)
        {
            if (isNpgsql)
            {
                string unit = bucket switch { SensorDataBucket.Minute => "minute", SensorDataBucket.Hour => "hour", _ => "day" };
                return $"date_trunc('{unit}', \"{column}\")";
            }
            string fmt = bucket switch
            {
                SensorDataBucket.Minute => "%Y-%m-%d %H:%i:00",
                SensorDataBucket.Hour => "%Y-%m-%d %H:00:00",
                _ => "%Y-%m-%d 00:00:00",
            };
            return $"CAST(DATE_FORMAT(`{column}`, '{fmt}') AS DATETIME)";
        }

        public async Task<string> SensorDataGetAsync(int? tenantID, int? deviceID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket)
        {
            if (!IsValidWindow(from, to))
            {
                return "";
            }

            bool npg = db.Database.IsNpgsql();
            string bucketExpr = BucketExpr(bucket, npg, "DateCreated");

            // CCS811 sentinel/outlier guard, CO2 column only - <=400 means "not warmed up yet" (not a real reading), >=8000 means a bad reading; nulling just this field leaves every other column of the picked (latest-in-bucket) row untouched. ROW_NUMBER()-per-bucket picks that latest row entirely in SQL - the app never sees the raw rows a bucket was chosen from.
            string sql = npg
                ? $$"""
                SELECT "Battery", "Temperature", "SoilTemperature", "Humidity", "Moisture", "Light",
                       CASE WHEN "Co2" <= 400 OR "Co2" >= 8000 THEN NULL ELSE "Co2" END AS "Co2",
                       "Tvoc", "Barometer", "LiquidPH", "RainLevel", "WaterLevel", "Wind", "Ec", "Weight",
                       "Bucket" AS "BucketStart"
                FROM (
                    SELECT "Battery", "Temperature", "SoilTemperature", "Humidity", "Moisture", "Light", "Co2", "Tvoc",
                           "Barometer", "LiquidPH", "RainLevel", "WaterLevel", "Wind", "Ec", "Weight",
                           {{bucketExpr}} AS "Bucket",
                           ROW_NUMBER() OVER (PARTITION BY {{bucketExpr}} ORDER BY "DateCreated" DESC) AS rn
                    FROM "dataSensor"
                    WHERE "DeviceID" = {0} AND ({1} IS NULL OR "TenantID" = {1})
                      AND "DateCreated" >= {2} AND "DateCreated" <= {3}
                ) t WHERE rn = 1 ORDER BY "BucketStart"
                """
                : $$"""
                SELECT Battery, Temperature, SoilTemperature, Humidity, Moisture, Light,
                       CASE WHEN Co2 <= 400 OR Co2 >= 8000 THEN NULL ELSE Co2 END AS Co2,
                       Tvoc, Barometer, LiquidPH, RainLevel, WaterLevel, Wind, Ec, Weight,
                       Bucket AS BucketStart
                FROM (
                    SELECT Battery, Temperature, SoilTemperature, Humidity, Moisture, Light, Co2, Tvoc,
                           Barometer, LiquidPH, RainLevel, WaterLevel, Wind, Ec, Weight,
                           {{bucketExpr}} AS Bucket,
                           ROW_NUMBER() OVER (PARTITION BY {{bucketExpr}} ORDER BY DateCreated DESC) AS rn
                    FROM dataSensor
                    WHERE DeviceID = {0} AND ({1} IS NULL OR TenantID = {1})
                      AND DateCreated >= {2} AND DateCreated <= {3}
                ) t WHERE rn = 1 ORDER BY BucketStart
                """;

            List<BucketedSensorRow> rows = await db.Database
                .SqlQueryRaw<BucketedSensorRow>(sql, (object?)deviceID ?? DBNull.Value, (object?)tenantID ?? DBNull.Value, from, to)
                .ToListAsync();

            return SensorReportShaper.Build(rows);
        }

        public Task<string> SensorDataZoneAverageGetAsync(int? tenantID, int deviceFarmUnitZoneID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket) =>
            AveragedSensorJsonAsync(tenantID, "FarmGreenhouseUnitZoneID", deviceFarmUnitZoneID, from, to, bucket);

        public Task<string> SensorDataUnitAverageGetAsync(int? tenantID, int deviceFarmUnitID, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket) =>
            AveragedSensorJsonAsync(tenantID, "FarmGreenhouseUnitID", deviceFarmUnitID, from, to, bucket);

        /// Shared by the zone/unit averaged-chart endpoints - scopeColumn is the REAL DB column name (FarmGreenhouseUnitID/FarmGreenhouseUnitZoneID, the legacy names DeviceFarmUnitID/DeviceFarmUnitZoneID are mapped to), never user input, so splicing it into the query string carries no injection risk.
        private async Task<string> AveragedSensorJsonAsync(int? tenantID, string scopeColumn, int scopeId, DateTimeOffset from, DateTimeOffset to, SensorDataBucket bucket)
        {
            if (!IsValidWindow(from, to))
            {
                return "";
            }

            bool npg = db.Database.IsNpgsql();
            string bucketExpr = BucketExpr(bucket, npg, "DateCreated");

            string sql = npg
                ? $$"""
                SELECT AVG("Battery") AS "Battery", AVG("Temperature") AS "Temperature", AVG("SoilTemperature") AS "SoilTemperature",
                       AVG("Humidity") AS "Humidity", AVG("Moisture") AS "Moisture", AVG("Light") AS "Light", AVG("Co2") AS "Co2",
                       AVG("Tvoc") AS "Tvoc", AVG("Barometer") AS "Barometer", AVG("LiquidPH") AS "LiquidPH", AVG("RainLevel") AS "RainLevel",
                       AVG("WaterLevel") AS "WaterLevel", AVG("Wind") AS "Wind", AVG("Ec") AS "Ec", AVG("Weight") AS "Weight",
                       {{bucketExpr}} AS "BucketStart"
                FROM "dataSensor"
                WHERE "{{scopeColumn}}" = {0} AND ({1} IS NULL OR "TenantID" = {1})
                  AND "DateCreated" >= {2} AND "DateCreated" <= {3}
                GROUP BY {{bucketExpr}}
                ORDER BY "BucketStart"
                """
                : $$"""
                SELECT AVG(Battery) AS Battery, AVG(Temperature) AS Temperature, AVG(SoilTemperature) AS SoilTemperature,
                       AVG(Humidity) AS Humidity, AVG(Moisture) AS Moisture, AVG(Light) AS Light, AVG(Co2) AS Co2,
                       AVG(Tvoc) AS Tvoc, AVG(Barometer) AS Barometer, AVG(LiquidPH) AS LiquidPH, AVG(RainLevel) AS RainLevel,
                       AVG(WaterLevel) AS WaterLevel, AVG(Wind) AS Wind, AVG(Ec) AS Ec, AVG(Weight) AS Weight,
                       {{bucketExpr}} AS BucketStart
                FROM dataSensor
                WHERE `{{scopeColumn}}` = {0} AND ({1} IS NULL OR TenantID = {1})
                  AND DateCreated >= {2} AND DateCreated <= {3}
                GROUP BY {{bucketExpr}}
                ORDER BY BucketStart
                """;

            List<AveragedSensorBucket> rows = await db.Database
                .SqlQueryRaw<AveragedSensorBucket>(sql, scopeId, (object?)tenantID ?? DBNull.Value, from, to)
                .ToListAsync();

            return SensorReportShaper.BuildAveraged(rows);
        }

        public async Task SensorDataDeleteAsync(int? tenantID, int? deviceID, DateTimeOffset olderThan)
        {
            await db.SensorData
                .Where(r => r.DeviceID == deviceID && r.TenantID == tenantID && r.DateCreated < olderThan)
                .ExecuteDeleteAsync();
        }

        private static readonly TimeSpan OptimizeBucketSize = TimeSpan.FromMinutes(5);

        /// OptimizeBucketSize doesn't align with date_trunc's minute/hour/day units, so this uses the FLOOR(epoch/N)*N technique directly (same technique BucketExpr's MySQL branch already uses via DATE_FORMAT, just for a bucket size neither provider has a named unit for).
        private static string FiveMinuteBucketExpr(bool isNpgsql, string column)
        {
            int seconds = (int)OptimizeBucketSize.TotalSeconds;
            return isNpgsql
                ? $"to_timestamp(floor(extract(epoch from \"{column}\") / {seconds}) * {seconds})"
                : $"FROM_UNIXTIME(FLOOR(UNIX_TIMESTAMP(`{column}`) / {seconds}) * {seconds})";
        }

        private static int? RoundToInt(double? value) => value.HasValue ? (int)Math.Round(value.Value, MidpointRounding.AwayFromZero) : null;

        /// Replaces old rows past cutoffUtc with one 5-minute-bucket-averaged row per device - the SQL AVG() aggregation itself is what keeps this bounded (a device's raw history collapses to a handful of bucket rows before it ever reaches the app); no outlier trimming anymore (that needed the raw values in the app to compute IQR bounds), a deliberate simplification now that this is a pure retention policy, not a job that loads rows.
        public async Task OptimizeOldSensorDataAsync(DateTime cutoffUtc, CancellationToken ct)
        {
            // Per-device, not one giant query - bounds each transaction's row count and lets a mid-run failure leave already-processed devices genuinely optimized instead of rolling everything back.
            List<int> deviceIds = await db.SensorData.AsNoTracking()
                .Where(r => r.DateCreated < cutoffUtc)
                .Select(r => r.DeviceID)
                .Distinct()
                .ToListAsync(ct);

            bool npg = db.Database.IsNpgsql();
            string bucketExpr = FiveMinuteBucketExpr(npg, "DateCreated");

            foreach (int deviceId in deviceIds)
            {
                ct.ThrowIfCancellationRequested();

                string sql = npg
                    ? $$"""
                    SELECT MAX("TenantID") AS "TenantID", MAX("FarmGreenhouseUnitID") AS "DeviceFarmUnitID", MAX("FarmGreenhouseUnitZoneID") AS "DeviceFarmUnitZoneID",
                           AVG("Battery") AS "Battery", AVG("Temperature") AS "Temperature", AVG("SoilTemperature") AS "SoilTemperature", AVG("Humidity") AS "Humidity",
                           AVG("Moisture") AS "Moisture", AVG("Light") AS "Light", AVG("Co2") AS "Co2", AVG("Tvoc") AS "Tvoc", AVG("Barometer") AS "Barometer",
                           AVG("LiquidPH") AS "LiquidPH", AVG("RainLevel") AS "RainLevel", AVG("WaterLevel") AS "WaterLevel", AVG("Wind") AS "Wind",
                           AVG("Ec") AS "Ec", AVG("Weight") AS "Weight", {{bucketExpr}} AS "BucketStart"
                    FROM "dataSensor"
                    WHERE "DeviceID" = {0} AND "DateCreated" < {1}
                    GROUP BY {{bucketExpr}}
                    """
                    : $$"""
                    SELECT MAX(TenantID) AS TenantID, MAX(FarmGreenhouseUnitID) AS DeviceFarmUnitID, MAX(FarmGreenhouseUnitZoneID) AS DeviceFarmUnitZoneID,
                           AVG(Battery) AS Battery, AVG(Temperature) AS Temperature, AVG(SoilTemperature) AS SoilTemperature, AVG(Humidity) AS Humidity,
                           AVG(Moisture) AS Moisture, AVG(Light) AS Light, AVG(Co2) AS Co2, AVG(Tvoc) AS Tvoc, AVG(Barometer) AS Barometer,
                           AVG(LiquidPH) AS LiquidPH, AVG(RainLevel) AS RainLevel, AVG(WaterLevel) AS WaterLevel, AVG(Wind) AS Wind,
                           AVG(Ec) AS Ec, AVG(Weight) AS Weight, {{bucketExpr}} AS BucketStart
                    FROM dataSensor
                    WHERE DeviceID = {0} AND DateCreated < {1}
                    GROUP BY {{bucketExpr}}
                    """;

                List<OptimizedSensorBucket> buckets = await db.Database
                    .SqlQueryRaw<OptimizedSensorBucket>(sql, deviceId, cutoffUtc)
                    .ToListAsync(ct);

                if (buckets.Count == 0)
                {
                    continue;
                }

                List<SensorDataRow> replacements = buckets.Select(b => new SensorDataRow
                {
                    TenantID = b.TenantID ?? 0,
                    DeviceID = deviceId,
                    DeviceFarmUnitID = b.DeviceFarmUnitID,
                    DeviceFarmUnitZoneID = b.DeviceFarmUnitZoneID,
                    Battery = RoundToInt(b.Battery),
                    Temperature = b.Temperature,
                    SoilTemperature = b.SoilTemperature,
                    Humidity = b.Humidity,
                    Moisture = RoundToInt(b.Moisture),
                    Light = RoundToInt(b.Light),
                    Co2 = RoundToInt(b.Co2),
                    Tvoc = RoundToInt(b.Tvoc),
                    Barometer = b.Barometer,
                    LiquidPH = b.LiquidPH,
                    RainLevel = RoundToInt(b.RainLevel),
                    WaterLevel = RoundToInt(b.WaterLevel),
                    Wind = RoundToInt(b.Wind),
                    Ec = b.Ec,
                    Weight = b.Weight,
                    DateCreated = b.BucketStart,
                }).ToList();

                // Delete-then-insert in one transaction - a crash between the two would otherwise duplicate or silently lose the bucket.
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.SensorData
                    .Where(r => r.DeviceID == deviceId && r.DateCreated < cutoffUtc)
                    .ExecuteDeleteAsync(ct);
                db.SensorData.AddRange(replacements);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
        }

        public async Task PurgeOldSensorDataAsync(DateTime cutoffUtc, bool shrinkAfterPurge, CancellationToken ct)
        {
            if (db.Database.IsNpgsql())
            {
                bool isHypertable;
                try
                {
                    isHypertable = await db.Database.SqlQueryRaw<int>(
                        "SELECT COUNT(*)::int FROM timescaledb_information.hypertables WHERE hypertable_name = 'dataSensor'")
                        .FirstAsync(ct) > 0;
                }
                catch (PostgresException)
                {
                    // TimescaleDB extension not installed - dataSensor is a plain table here (like MariaDB, minus the OPTIMIZE-TABLE shrink step below).
                    isHypertable = false;
                }

                if (isHypertable)
                {
                    // drop_chunks deletes whole chunk files (space returned immediately, unlike DELETE) - the embedded double-quotes keep the regclass cast from lowercasing this mixed-case table name.
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""SELECT drop_chunks('"dataSensor"'::regclass, older_than => {cutoffUtc});""", ct);
                    return;
                }

                await db.SensorData.Where(r => r.DateCreated < cutoffUtc).ExecuteDeleteAsync(ct);
                return;
            }

            // A single unbatched DELETE across a multi-million-row table holds its lock for the whole run - MySQL supports DELETE...LIMIT natively (EF's ExecuteDeleteAsync can't express it), so loop in PurgeBatchSize chunks with a short pause between them instead.
            int deletedRows;
            do
            {
                deletedRows = await db.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM `dataSensor` WHERE `DateCreated` < {cutoffUtc} LIMIT {PurgeBatchSize}", ct);
                if (deletedRows == PurgeBatchSize)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200), ct);
                }
            } while (deletedRows == PurgeBatchSize);

            if (shrinkAfterPurge)
            {
                // InnoDB never shrinks its .ibd file on a plain DELETE - OPTIMIZE TABLE is the locking rebuild that actually returns space, only run when the admin opts in since it can take a long time.
                await db.Database.ExecuteSqlRawAsync("OPTIMIZE TABLE `dataSensor`;", ct);
            }
        }

        private const int PurgeBatchSize = 10_000;

        public async Task<IList<SensorData>> SensorDataExportGetAsync(int tenantID, DateTime? sinceUtc)
        {
            // Dirty reads are fine for an export snapshot - avoids InnoDB gap-locking a live device's concurrent SensorDataPushAsync inserts on what can be a huge table (Postgres treats this as ReadCommitted regardless, MVCC readers never block writers there anyway).
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadUncommitted);

            IQueryable<SensorDataRow> q = db.SensorData.AsNoTracking().Where(s => s.TenantID == tenantID);
            if (sinceUtc is DateTime since)
            {
                q = q.Where(s => s.DateCreated >= since);
            }
            return await q.Select(s => new SensorData
            {
                TenantID = s.TenantID,
                DeviceID = s.DeviceID,
                DeviceFarmUnitID = s.DeviceFarmUnitID,
                DeviceFarmUnitZoneID = s.DeviceFarmUnitZoneID,
                Battery = s.Battery,
                Temperature = s.Temperature,
                SoilTemperature = s.SoilTemperature,
                Humidity = s.Humidity,
                Moisture = s.Moisture,
                Light = s.Light,
                Co2 = s.Co2,
                Tvoc = s.Tvoc,
                Barometer = s.Barometer,
                LiquidPH = s.LiquidPH,
                RainLevel = s.RainLevel,
                WaterLevel = s.WaterLevel,
                Wind = s.Wind,
                Ec = s.Ec,
                Weight = s.Weight,
                WifiRssiDbm = s.WifiRssiDbm,
                LoRaRssiDbm = s.LoRaRssiDbm,
                LoRaSnrDb = s.LoRaSnrDb,
                DateCreated = s.DateCreated ?? default,
            }).ToListAsync();
        }

        public IQueryable<SensorDataODataEntry> SensorDataODataQueryable(int tenantID) =>
            db.SensorData.AsNoTracking().Where(s => s.TenantID == tenantID).Select(s => new SensorDataODataEntry
            {
                IDSensorData = s.IDSensorData,
                DeviceID = s.DeviceID,
                DeviceFarmUnitID = s.DeviceFarmUnitID,
                DeviceFarmUnitZoneID = s.DeviceFarmUnitZoneID,
                Battery = s.Battery,
                Temperature = s.Temperature,
                SoilTemperature = s.SoilTemperature,
                Humidity = s.Humidity,
                Moisture = s.Moisture,
                Light = s.Light,
                Co2 = s.Co2,
                Tvoc = s.Tvoc,
                Barometer = s.Barometer,
                LiquidPH = s.LiquidPH,
                RainLevel = s.RainLevel,
                WaterLevel = s.WaterLevel,
                Wind = s.Wind,
                Ec = s.Ec,
                Weight = s.Weight,
                WifiRssiDbm = s.WifiRssiDbm,
                LoRaRssiDbm = s.LoRaRssiDbm,
                LoRaSnrDb = s.LoRaSnrDb,
                DateCreated = s.DateCreated,
            });

        public async Task SensorDataImportAsync(IList<SensorData> rows)
        {
            db.SensorData.AddRange(rows.Select(r => new SensorDataRow
            {
                TenantID = r.TenantID ?? 0,
                DeviceID = r.DeviceID ?? 0,
                DeviceFarmUnitID = r.DeviceFarmUnitID,
                DeviceFarmUnitZoneID = r.DeviceFarmUnitZoneID,
                Battery = r.Battery,
                Temperature = r.Temperature,
                SoilTemperature = r.SoilTemperature,
                Humidity = r.Humidity,
                Moisture = r.Moisture,
                Light = r.Light,
                Co2 = r.Co2,
                Tvoc = r.Tvoc,
                Barometer = r.Barometer,
                LiquidPH = r.LiquidPH,
                RainLevel = r.RainLevel,
                WaterLevel = r.WaterLevel,
                Wind = (int?)r.Wind, // SensorDataRow.Wind is int, Agrumy.Shared.Models.SensorData.Wind is double
                Ec = r.Ec,
                Weight = r.Weight,
                WifiRssiDbm = r.WifiRssiDbm,
                LoRaRssiDbm = r.LoRaRssiDbm,
                LoRaSnrDb = r.LoRaSnrDb,
                DateCreated = r.DateCreated,
            }));
            await db.SaveChangesAsync();
        }

        // AssumeUniversal: a bare "yyyy-MM-dd HH:mm:ss" (no Z/offset, the device's own format) is UTC, not the host's local zone - the implicit DateTime->DateTimeOffset conversion on SensorDataRow.DateCreated otherwise reinterprets Kind=Unspecified as local time.
        private static DateTime? ReadDateTime(string? s) =>
            DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt) ? dt : null;
    }
}
