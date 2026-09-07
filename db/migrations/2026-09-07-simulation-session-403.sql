-- Roadmap #403: "Add Simulation" becomes the entry point (session/container a device is added TO),
-- replacing the old per-device toggle mental model - devices (physical or virtual) join a named,
-- time-boxed session (max 48h) instead of being independently toggled. A physical device's actual
-- override values still live in `deviceSimulation` (#251); this just owns turning that on/off and,
-- via SimulationSessionExpiryEvaluator, the hard 48h safety cutoff.
--
-- WHY THIS IS MANUAL: see 2026-08-30-event-log-columns.sql - EnsureSchemaAsync() only creates
-- tables in a brand-new (zero-table) database, never adds a table to one that already exists.
--
-- SAFE TO RE-RUN: CREATE TABLE IF NOT EXISTS.

CREATE TABLE IF NOT EXISTS `simulationSession` (
  `IDSimulationSession` INT(11) NOT NULL AUTO_INCREMENT,
  `TenantID` INT(11) NOT NULL,
  `Name` VARCHAR(128) DEFAULT NULL,
  `StartedAtUtc` DATETIME(6) NOT NULL,
  `ExpiresAtUtc` DATETIME(6) NOT NULL,
  `StoppedAtUtc` DATETIME(6) DEFAULT NULL,
  PRIMARY KEY (`IDSimulationSession`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `simulationSessionDevice` (
  `IDSimulationSession` INT(11) NOT NULL,
  `DeviceID` INT(11) NOT NULL,
  PRIMARY KEY (`IDSimulationSession`, `DeviceID`),
  CONSTRAINT `FK_simulationSessionDevice_session` FOREIGN KEY (`IDSimulationSession`) REFERENCES `simulationSession` (`IDSimulationSession`),
  CONSTRAINT `FK_simulationSessionDevice_device` FOREIGN KEY (`DeviceID`) REFERENCES `device` (`IDDevice`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Backfill: any virtual device registered before this migration (deviceVirtual with no session
-- membership) gets a generous 48h catch-up session per tenant, so VirtualDeviceRunnerBackgroundService's
-- new session-gated check doesn't suddenly go idle for devices an admin was actively relying on -
-- they naturally expire via the same 48h cutoff like any other session afterward.
INSERT INTO `simulationSession` (`TenantID`, `Name`, `StartedAtUtc`, `ExpiresAtUtc`)
SELECT DISTINCT d.TenantID, 'Pre-existing virtual devices (auto-migrated)', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6) + INTERVAL 48 HOUR
FROM `deviceVirtual` dv
JOIN `device` d ON d.IDDevice = dv.DeviceID
WHERE NOT EXISTS (SELECT 1 FROM `simulationSessionDevice` sd WHERE sd.DeviceID = dv.DeviceID);

INSERT INTO `simulationSessionDevice` (`IDSimulationSession`, `DeviceID`)
SELECT s.IDSimulationSession, dv.DeviceID
FROM `deviceVirtual` dv
JOIN `device` d ON d.IDDevice = dv.DeviceID
JOIN `simulationSession` s ON s.TenantID = d.TenantID AND s.Name = 'Pre-existing virtual devices (auto-migrated)'
WHERE NOT EXISTS (SELECT 1 FROM `simulationSessionDevice` sd WHERE sd.DeviceID = dv.DeviceID);

-- Sanity check after running:
--   SHOW CREATE TABLE `simulationSession`;
--   SHOW CREATE TABLE `simulationSessionDevice`;
--   SELECT * FROM `simulationSessionDevice`;
