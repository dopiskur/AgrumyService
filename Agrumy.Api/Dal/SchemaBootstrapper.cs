using Agrumy.Dal;
using Agrumy.Api.Dal.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Agrumy.Api.Dal
{
    /// ISystemRepository implementation - migrates/verifies the schema at startup and delegates row seeding to DataSeeder; split out of the former EfRepository once its other facets moved to standalone EfXxxRepository classes.
    internal sealed class SchemaBootstrapper(AgrumyDbContext db, ILogger<SchemaBootstrapper> logger, IServerConfigRepository serverConfigRepository, DataSeeder dataSeeder) : ISystemRepository
    {
        public async Task<bool> TestConnectionAsync()
        {
            return await db.Database.CanConnectAsync();
        }

        public async Task EnsureSchemaAsync()
        {
            await MarkLegacyEnsureCreatedSchemaAsBaselineAsync(db);

            // A brand-new DB gets every migration from empty; invent.hr's __EFMigrationsHistory was seeded with InitialBeta as already-applied (its schema already matched), so this is a no-op there until a real future migration ships.
            await db.Database.MigrateAsync();

            await EnsureTimescaleHypertableAsync();

            await dataSeeder.SeedAsync(db);
        }

        /// Any DB created by the old EnsureCreatedAsync path has the full schema but no __EFMigrationsHistory row, so MigrateAsync would try to CREATE TABLE against tables that already exist - detect that case via the "device" table and mark the baseline migration applied first.
        private async Task MarkLegacyEnsureCreatedSchemaAsBaselineAsync(AgrumyDbContext db)
        {
            var historyRepository = db.GetService<IHistoryRepository>();
            if (await historyRepository.ExistsAsync())
            {
                return;
            }

            var connection = db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            // MySQL's information_schema.tables spans EVERY database on the server, not just the connected one - unscoped, a second database elsewhere with its own "device" table (e.g. an old agrumyapi alongside a new one) gives a false positive here. Postgres already scopes information_schema.tables to the connected database by connection design, but a non-default schema could still collide, so the same current_schema() filter is applied there too.
            command.CommandText = db.Database.IsMySql()
                ? "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'device' AND table_schema = DATABASE()"
                : "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'device' AND table_schema = current_schema()";
            var deviceTableExists = Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
            if (!deviceTableExists)
            {
                return;
            }

            // ALL currently-known migrations, not just the first: EnsureCreatedAsync always builds from the model snapshot as of whatever code version last ran it, so a legacy DB detected here already has every column every migration up to that point would have added - marking only the first (InitialBeta) leaves every later migration to run ADD COLUMN against columns that already exist.
            var allMigrationIds = db.Database.GetMigrations().ToList();
            var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "9.0.0";
            await db.Database.ExecuteSqlRawAsync(historyRepository.GetCreateIfNotExistsScript());
            foreach (var migrationId in allMigrationIds)
            {
                await db.Database.ExecuteSqlRawAsync(historyRepository.GetInsertScript(new HistoryRow(migrationId, productVersion)));
            }
            logger.LogWarning("Legacy EnsureCreated schema detected (device table exists, no migrations history) - marked {Count} migrations up to {LastMigrationId} as already applied", allMigrationIds.Count, allMigrationIds[^1]);
        }

        /// TimescaleDB requires the partitioning column in every unique constraint including the PK, so this widens dataSensor's PK from IDSensorData alone to (IDSensorData, DateCreated) - no-op on MySQL/Pomelo.
        private async Task EnsureTimescaleHypertableAsync()
        {
            if (!db.Database.IsNpgsql())
            {
                return;
            }

            try
            {
                await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS timescaledb;");
            }
            catch (PostgresException ex)
            {
                logger.LogWarning(ex,
                    "TimescaleDB extension unavailable on this PostgreSQL server; dataSensor stays a plain table.");
                return;
            }

            // One DO block, not two separate calls - the PK rename and create_hypertable() must both happen (or neither) exactly once.
            const string sql = """
                DO $$
                DECLARE
                  pk_name text;
                BEGIN
                  IF NOT EXISTS (
                    SELECT 1 FROM timescaledb_information.hypertables WHERE hypertable_name = 'dataSensor'
                  ) THEN
                    SELECT conname INTO pk_name FROM pg_constraint
                      WHERE conrelid = '"dataSensor"'::regclass AND contype = 'p';
                    IF pk_name IS NOT NULL THEN
                      EXECUTE format('ALTER TABLE %I DROP CONSTRAINT %I', 'dataSensor', pk_name);
                    END IF;
                    ALTER TABLE "dataSensor" ADD PRIMARY KEY ("IDSensorData", "DateCreated");
                    -- create_hypertable's first parameter is REGCLASS: an unquoted literal here folds to lowercase and misses this mixed-case table - the embedded double quotes below make it match "dataSensor" exactly.
                    PERFORM create_hypertable('"dataSensor"', 'DateCreated', migrate_data => true, if_not_exists => true);
                  END IF;
                END $$;
                """;
            await db.Database.ExecuteSqlRawAsync(sql);

            await serverConfigRepository.ApplyRetentionPolicyAsync((await serverConfigRepository.ServerConfigGetAsync(1)).SensorDataRetentionDays);
        }

        public DbFailureKind ClassifyException(Exception ex) => DbExceptionClassifier.Classify(ex);
    }
}
