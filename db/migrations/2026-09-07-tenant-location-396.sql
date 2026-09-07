-- Roadmap #396(6): astronomical (sunrise/sunset) schedule conditions used ServerConfig's
-- single server-wide Latitude/Longitude for every tenant, same problem timezone had before #290.
-- These are optional, per-tenant overrides - AstronomicalRuleResolver falls back to
-- ServerConfig.WeatherLocationLat/Lon (unchanged) when a tenant hasn't set its own.
--
-- SAFE TO RE-RUN: ADD COLUMN IF NOT EXISTS.

ALTER TABLE `tenant`
  ADD COLUMN IF NOT EXISTS `Latitude` DOUBLE NULL AFTER `ScheduleTimeZone`,
  ADD COLUMN IF NOT EXISTS `Longitude` DOUBLE NULL AFTER `Latitude`;

-- Sanity check after running:
--   SHOW COLUMNS FROM `tenant`;
