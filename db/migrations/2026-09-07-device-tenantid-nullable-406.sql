-- Roadmap #406: Device.TenantID becomes nullable (reversing the earlier non-nullable decision,
-- per explicit user instruction overriding the #397(1) precedent). TenantID=0 is STILL a real
-- tenant (the bootstrap/default one) - this migration does NOT touch any existing row's value,
-- it only lifts the NOT NULL/DEFAULT 0 constraint so a genuinely tenant-less device (one
-- registered under a user with no tenant) can be stored as NULL instead of being silently
-- collapsed into tenant 0's identity (see DeviceApiController.DeviceRegistration).
--
-- WHY THIS IS MANUAL: see 2026-08-30-event-log-columns.sql - EnsureSchemaAsync() only creates
-- tables in a brand-new (zero-table) database, never alters a column on one that already exists.
--
-- SAFE TO RE-RUN: MODIFY COLUMN is idempotent (repeating it is a no-op once already nullable).

ALTER TABLE `device`
  MODIFY COLUMN `TenantID` INT(11) NULL DEFAULT NULL;

-- Sanity check after running:
--   SHOW COLUMNS FROM `device` LIKE 'TenantID';
