-- Roadmap #393 finding (6): WaterPumpMaxRunSeconds only ever capped how long WaterPump could run,
-- never whether the tank had any water left - Interval/Schedule/Manual modes ran regardless, since
-- none of them consult WaterLevel on their own. Adds a per-zone dry-run threshold, evaluated
-- device-side against the zone's existing WaterLevelRawEmpty/Full calibration (roadmap #234).
--
-- WHY THIS IS MANUAL: see 2026-08-30-event-log-columns.sql - EnsureSchemaAsync() only creates
-- tables in a brand-new (zero-table) database, never adds a column to one that already exists.
--
-- SAFE TO RE-RUN: ADD COLUMN uses IF NOT EXISTS.

ALTER TABLE `deviceFarmUnitZone`
  ADD COLUMN IF NOT EXISTS `WaterPumpMinLevel` DOUBLE DEFAULT NULL;

-- Sanity check after running:
--   SHOW COLUMNS FROM `deviceFarmUnitZone` LIKE 'WaterPumpMinLevel';
