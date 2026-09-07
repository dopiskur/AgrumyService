-- Roadmap #409: soft delete for Farm and Device (same mechanism cascades into DeviceFarmUnit/
-- DeviceFarmUnitZone when a Farm is deleted, see roadmap #408). A "deleted" row stays in the table,
-- filtered out of every ordinary query by AgrumyDbContext's HasQueryFilter, and stays visible to
-- RecycleBinApiController for the retention window in serverConfig.RecycleBinRetentionDays.
-- SensorData is NOT touched here - it stays pointed at the (now hidden) device indefinitely; a
-- separate manual/schedulable "Purge orphaned sensor data" action (PurgeOrphanedSensorDataEvaluator)
-- is what actually removes it, independent of the recycle bin window.
--
-- WHY THIS IS MANUAL: see 2026-08-30-event-log-columns.sql - EnsureSchemaAsync() only creates
-- tables in a brand-new (zero-table) database, never adds a column to one that already exists.
--
-- SAFE TO RE-RUN: ADD COLUMN uses IF NOT EXISTS.

ALTER TABLE `device`
  ADD COLUMN IF NOT EXISTS `Deleted` TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `DeletedAtUtc` DATETIME(6) DEFAULT NULL;

ALTER TABLE `deviceFarm`
  ADD COLUMN IF NOT EXISTS `Deleted` TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `DeletedAtUtc` DATETIME(6) DEFAULT NULL;

ALTER TABLE `deviceFarmUnit`
  ADD COLUMN IF NOT EXISTS `Deleted` TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `DeletedAtUtc` DATETIME(6) DEFAULT NULL;

ALTER TABLE `deviceFarmUnitZone`
  ADD COLUMN IF NOT EXISTS `Deleted` TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `DeletedAtUtc` DATETIME(6) DEFAULT NULL;

ALTER TABLE `serverConfig`
  ADD COLUMN IF NOT EXISTS `RecycleBinRetentionDays` INT DEFAULT 30,
  ADD COLUMN IF NOT EXISTS `PurgeOrphanedSensorDataScheduleEnabled` TINYINT(1) NOT NULL DEFAULT 0;

-- Sanity check after running:
--   SHOW COLUMNS FROM `device` LIKE 'Deleted%';
--   SHOW COLUMNS FROM `deviceFarm` LIKE 'Deleted%';
--   SHOW COLUMNS FROM `deviceFarmUnit` LIKE 'Deleted%';
--   SHOW COLUMNS FROM `deviceFarmUnitZone` LIKE 'Deleted%';
--   SHOW COLUMNS FROM `serverConfig` LIKE 'RecycleBinRetentionDays';
--   SHOW COLUMNS FROM `serverConfig` LIKE 'PurgeOrphanedSensorDataScheduleEnabled';
