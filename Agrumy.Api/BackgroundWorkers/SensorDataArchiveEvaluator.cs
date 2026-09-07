using Agrumy.Dal;
using Agrumy.Dal.Entities;
using Agrumy.Api.Dal.Interface;
using Agrumy.Shared.Models;
using Agrumy.Api.Utils;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Agrumy.Api.BackgroundWorkers
{
    /// Roadmap #209 - MariaDB/MySQL only (Postgres/TimescaleDB uses its own native tiered storage instead, #14). Moves sensorData rows past ArchiveCutoffCalculator's cutoff into a separate, admin-configured archive database instead of deleting them (contrast SensorDataRetentionEvaluator, which deletes).
    public sealed class SensorDataArchiveEvaluator(AgrumyDbContext db, IServerConfigRepository serverConfigRepo, ILogger<SensorDataArchiveEvaluator> logger)
    {
        private const int BatchSize = 2000;

        public async Task RunOnceAsync(CancellationToken ct = default)
        {
            if (db.Database.IsNpgsql())
            {
                return;
            }

            ServerConfig config = await serverConfigRepo.ServerConfigGetAsync(1);
            if (!config.ArchiveEnabled || string.IsNullOrWhiteSpace(config.ArchiveHost) || string.IsNullOrWhiteSpace(config.ArchiveDatabaseName)
                || string.IsNullOrWhiteSpace(config.ArchiveUsername) || string.IsNullOrEmpty(config.ArchivePassword))
            {
                return;
            }

            DateTimeOffset? cutoffUtc = ArchiveCutoffCalculator.ComputeCutoffUtc(config, DateTimeOffset.UtcNow);
            if (cutoffUtc is null)
            {
                return; // Custom mode selected but its date/rolling-days value isn't set yet
            }

            string archiveConnectionString = new MySqlConnectionStringBuilder
            {
                Server = config.ArchiveHost,
                Port = (uint)(config.ArchivePort ?? 3306),
                Database = config.ArchiveDatabaseName,
                UserID = config.ArchiveUsername,
                Password = config.ArchivePassword,
                SslMode = MySqlSslMode.Preferred,
            }.ConnectionString;

            await using var archiveConnection = new MySqlConnection(archiveConnectionString);
            try
            {
                await archiveConnection.OpenAsync(ct);
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(ex, "SensorDataArchiveEvaluator: could not connect to the archive database - skipping this run.");
                }
                return;
            }

            await EnsureArchiveTableAsync(archiveConnection, ct);

            int totalMoved = 0;
            while (true)
            {
                List<SensorDataRow> batch = await db.SensorData.AsNoTracking()
                    .Where(r => r.DateCreated < cutoffUtc)
                    .OrderBy(r => r.IDSensorData)
                    .Take(BatchSize)
                    .ToListAsync(ct);
                if (batch.Count == 0)
                {
                    break;
                }

                await InsertBatchIgnoringDuplicatesAsync(archiveConnection, batch, ct);

                List<int> ids = batch.Select(r => r.IDSensorData).ToList();
                await db.SensorData.Where(r => ids.Contains(r.IDSensorData)).ExecuteDeleteAsync(ct);
                totalMoved += batch.Count;

                if (batch.Count < BatchSize)
                {
                    break;
                }
            }

            if (totalMoved > 0 && logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("SensorDataArchiveEvaluator: moved {Count} sensorData rows older than {CutoffUtc:O} to the archive database.", totalMoved, cutoffUtc);
            }
            await serverConfigRepo.ServerConfigArchiveRunStateSetAsync(DateTimeOffset.UtcNow, 1);
        }

        /// Same shape as SensorDataRow - the archive database starts out empty, admin-provisioned, so the table has to be created on first use rather than assumed to already exist.
        private static async Task EnsureArchiveTableAsync(MySqlConnection connection, CancellationToken ct)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS `sensorData` (
                  `IDSensorData` INT NOT NULL PRIMARY KEY,
                  `TenantID` INT NOT NULL,
                  `DeviceID` INT NOT NULL,
                  `DeviceFarmUnitID` INT NULL,
                  `DeviceFarmUnitZoneID` INT NULL,
                  `Battery` INT NULL,
                  `Temperature` DOUBLE NULL,
                  `SoilTemperature` DOUBLE NULL,
                  `Humidity` DOUBLE NULL,
                  `Moisture` INT NULL,
                  `Light` INT NULL,
                  `Co2` INT NULL,
                  `Tvoc` INT NULL,
                  `Barometer` DOUBLE NULL,
                  `LiquidPH` DOUBLE NULL,
                  `RainLevel` INT NULL,
                  `WaterLevel` INT NULL,
                  `Wind` INT NULL,
                  `DateCreated` DATETIME(6) NULL
                )
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// INSERT IGNORE keyed on the primary key makes the whole move idempotent - a crash between this insert and the matching DELETE on the active side (below) just re-inserts the same, already-present rows as a no-op on the next run instead of duplicating them. One transaction per batch, not one per row, so a batch either fully lands or fully rolls back.
        private static async Task InsertBatchIgnoringDuplicatesAsync(MySqlConnection connection, List<SensorDataRow> batch, CancellationToken ct)
        {
            await using MySqlTransaction transaction = await connection.BeginTransactionAsync(ct);
            await using MySqlCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT IGNORE INTO `sensorData`
                  (`IDSensorData`,`TenantID`,`DeviceID`,`DeviceFarmUnitID`,`DeviceFarmUnitZoneID`,`Battery`,`Temperature`,`SoilTemperature`,`Humidity`,`Moisture`,`Light`,`Co2`,`Tvoc`,`Barometer`,`LiquidPH`,`RainLevel`,`WaterLevel`,`Wind`,`DateCreated`)
                VALUES
                  (@IDSensorData,@TenantID,@DeviceID,@DeviceFarmUnitID,@DeviceFarmUnitZoneID,@Battery,@Temperature,@SoilTemperature,@Humidity,@Moisture,@Light,@Co2,@Tvoc,@Barometer,@LiquidPH,@RainLevel,@WaterLevel,@Wind,@DateCreated)
                """;
            cmd.Parameters.Add("@IDSensorData", MySqlDbType.Int32);
            cmd.Parameters.Add("@TenantID", MySqlDbType.Int32);
            cmd.Parameters.Add("@DeviceID", MySqlDbType.Int32);
            cmd.Parameters.Add("@DeviceFarmUnitID", MySqlDbType.Int32);
            cmd.Parameters.Add("@DeviceFarmUnitZoneID", MySqlDbType.Int32);
            cmd.Parameters.Add("@Battery", MySqlDbType.Int32);
            cmd.Parameters.Add("@Temperature", MySqlDbType.Double);
            cmd.Parameters.Add("@SoilTemperature", MySqlDbType.Double);
            cmd.Parameters.Add("@Humidity", MySqlDbType.Double);
            cmd.Parameters.Add("@Moisture", MySqlDbType.Int32);
            cmd.Parameters.Add("@Light", MySqlDbType.Int32);
            cmd.Parameters.Add("@Co2", MySqlDbType.Int32);
            cmd.Parameters.Add("@Tvoc", MySqlDbType.Int32);
            cmd.Parameters.Add("@Barometer", MySqlDbType.Double);
            cmd.Parameters.Add("@LiquidPH", MySqlDbType.Double);
            cmd.Parameters.Add("@RainLevel", MySqlDbType.Int32);
            cmd.Parameters.Add("@WaterLevel", MySqlDbType.Int32);
            cmd.Parameters.Add("@Wind", MySqlDbType.Int32);
            cmd.Parameters.Add("@DateCreated", MySqlDbType.DateTime);
            await cmd.PrepareAsync(ct);

            foreach (SensorDataRow row in batch)
            {
                cmd.Parameters["@IDSensorData"].Value = row.IDSensorData;
                cmd.Parameters["@TenantID"].Value = row.TenantID;
                cmd.Parameters["@DeviceID"].Value = row.DeviceID;
                cmd.Parameters["@DeviceFarmUnitID"].Value = (object?)row.DeviceFarmUnitID ?? DBNull.Value;
                cmd.Parameters["@DeviceFarmUnitZoneID"].Value = (object?)row.DeviceFarmUnitZoneID ?? DBNull.Value;
                cmd.Parameters["@Battery"].Value = (object?)row.Battery ?? DBNull.Value;
                cmd.Parameters["@Temperature"].Value = (object?)row.Temperature ?? DBNull.Value;
                cmd.Parameters["@SoilTemperature"].Value = (object?)row.SoilTemperature ?? DBNull.Value;
                cmd.Parameters["@Humidity"].Value = (object?)row.Humidity ?? DBNull.Value;
                cmd.Parameters["@Moisture"].Value = (object?)row.Moisture ?? DBNull.Value;
                cmd.Parameters["@Light"].Value = (object?)row.Light ?? DBNull.Value;
                cmd.Parameters["@Co2"].Value = (object?)row.Co2 ?? DBNull.Value;
                cmd.Parameters["@Tvoc"].Value = (object?)row.Tvoc ?? DBNull.Value;
                cmd.Parameters["@Barometer"].Value = (object?)row.Barometer ?? DBNull.Value;
                cmd.Parameters["@LiquidPH"].Value = (object?)row.LiquidPH ?? DBNull.Value;
                cmd.Parameters["@RainLevel"].Value = (object?)row.RainLevel ?? DBNull.Value;
                cmd.Parameters["@WaterLevel"].Value = (object?)row.WaterLevel ?? DBNull.Value;
                cmd.Parameters["@Wind"].Value = (object?)row.Wind ?? DBNull.Value;
                cmd.Parameters["@DateCreated"].Value = (object?)row.DateCreated?.UtcDateTime ?? DBNull.Value;
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
    }
}
