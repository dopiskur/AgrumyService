-- Roadmap #396(4): recursive ConditionNode tree replaces the flat Conditions[]+ops[] fold entirely
-- (alfa phase, no backward compat) - a rule's conditions can now span several metrics and nest
-- arbitrary AND/OR groups. SensorMetric moves off the rule itself (each ComparisonNode inside the
-- tree carries its own now) onto nothing - it's simply removed. Name/Description are new required/
-- optional fields; IsSafetyRule is #396(5)'s hierarchy-override-survival flag.
--
-- deviceFarmUnitZoneRule had 0 rows at the time of writing (checked before running) - a clean
-- column swap, no content to migrate.
--
-- SAFE TO RE-RUN: ADD/DROP COLUMN IF (NOT) EXISTS.

ALTER TABLE `deviceFarmUnitZoneRule`
  DROP COLUMN IF EXISTS `SensorMetric`;

ALTER TABLE `deviceFarmUnitZoneRule`
  CHANGE COLUMN `Conditions` `RootConditionJson` TEXT NOT NULL;

ALTER TABLE `deviceFarmUnitZoneRule`
  ADD COLUMN IF NOT EXISTS `Name` VARCHAR(100) NOT NULL DEFAULT '' AFTER `ActionType`,
  ADD COLUMN IF NOT EXISTS `Description` TEXT NULL AFTER `Name`,
  ADD COLUMN IF NOT EXISTS `IsSafetyRule` TINYINT(1) NOT NULL DEFAULT 0 AFTER `RootConditionJson`;

-- Sanity check after running:
--   SHOW COLUMNS FROM `deviceFarmUnitZoneRule`;
