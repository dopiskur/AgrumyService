-- Roadmap #209: opt-in MariaDB/MySQL-only archiving to a separate admin-configured database (Postgres/
-- TimescaleDB installs use their own native tiered storage instead, #14) - moves sensorData rows past
-- the configured cutoff to that database instead of deleting them. Inactive until an admin sets and
-- successfully tests archive credentials (ServerConfigApiController.SaveArchiveSettings).
--
-- WHY THIS IS MANUAL: see 2026-08-30-event-log-columns.sql - EnsureSchemaAsync() only creates tables
-- in a brand-new (zero-table) database, never adds a column to one that already exists.
--
-- SAFE TO RE-RUN: every ADD COLUMN uses IF NOT EXISTS.

ALTER TABLE `serverConfig`
  ADD COLUMN IF NOT EXISTS `ArchiveEnabled` TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `ArchiveCutoffMode` INT(11) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `ArchiveCustomCutoffDate` DATE DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS `ArchiveCustomRollingDays` INT(11) DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS `ArchiveHost` VARCHAR(255) DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS `ArchivePort` INT(11) DEFAULT 3306,
  ADD COLUMN IF NOT EXISTS `ArchiveDatabaseName` VARCHAR(64) DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS `ArchiveUsername` VARCHAR(128) DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS `ArchivePassword` VARCHAR(512) DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS `ArchiveLastRunAtUtc` DATETIME(6) DEFAULT NULL;

-- Sanity check after running:
--   SHOW COLUMNS FROM `serverConfig` LIKE 'Archive%';
