-- Roadmap #238: admin-arranged dashboard widgets, per zone - sensor value tiles, sensor trend
-- sparklines, relay status tiles, and plain text labels, in display order. JSON blob at the
-- application layer (see api.Models.DeviceFarmUnitZone.DashboardWidgets), same convention as
-- deviceFarmUnitZoneRule.RootConditionJson - chosen (per this roadmap item's own note) so a future
-- mobile client can render the exact same server-defined layout, not a separate mobile-only design.
--
-- WHY THIS IS MANUAL: see 2026-08-30-event-log-columns.sql - EnsureSchemaAsync() only creates
-- tables in a brand-new (zero-table) database, never adds a column to one that already exists.
--
-- SAFE TO RE-RUN: ADD COLUMN uses IF NOT EXISTS.

ALTER TABLE `deviceFarmUnitZone`
  ADD COLUMN IF NOT EXISTS `DashboardWidgetsJson` TEXT DEFAULT NULL;

-- Sanity check after running:
--   SHOW COLUMNS FROM `deviceFarmUnitZone` LIKE 'DashboardWidgetsJson';
