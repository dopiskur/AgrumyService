-- Roadmap #412 (c): one-time backfill mirroring EfDeviceFarmUnitRepository.EnsureFirstFarmAsync
-- (now called on every tenant-creation path going forward) for tenants that already existed before
-- this - creates "First farm" for any tenant with none, then sweeps that tenant's currently-
-- unassigned units (DeviceFarmID IS NULL, excluding the global sentinel unit 0) into it.
--
-- WHY THIS IS MANUAL: see 2026-08-30-event-log-columns.sql - EnsureSchemaAsync() only creates
-- tables in a brand-new (zero-table) database, it never backfills data into one that already exists.
--
-- SAFE TO RE-RUN: both statements are already-has-a-farm/already-assigned guarded.

INSERT INTO `deviceFarm` (`TenantID`, `DeviceFarmName`)
SELECT t.`IDTenant`, 'First farm'
FROM `tenant` t
WHERE NOT EXISTS (SELECT 1 FROM `deviceFarm` f WHERE f.`TenantID` = t.`IDTenant`);

UPDATE `deviceFarmUnit` u
JOIN `deviceFarm` f ON f.`TenantID` = u.`TenantID` AND f.`DeviceFarmName` = 'First farm'
SET u.`DeviceFarmID` = f.`IDDeviceFarm`
WHERE u.`DeviceFarmID` IS NULL AND u.`IDDeviceFarmUnit` != 0;

-- Sanity check after running:
--   SELECT t.IDTenant, t.TenantName FROM tenant t WHERE NOT EXISTS (SELECT 1 FROM deviceFarm f WHERE f.TenantID = t.IDTenant);  -- expect 0 rows
--   SELECT * FROM deviceFarmUnit WHERE DeviceFarmID IS NULL AND IDDeviceFarmUnit != 0;  -- expect 0 rows (unless a unit's tenant genuinely has no rows in `tenant` - shouldn't happen)
