-- Roadmap #395 finding 3: LoRa private-protocol uplinks were plaintext and unauthenticated -
-- spoofable telemetry, forgeable/suppressible alarms. Adds per-device AES-256-GCM key storage
-- (DeviceApiController.LoRaPrivateKeyGenerate mints one, admin copies it into the node's own
-- loraPrivateRegistration.json) and the replay-protection counter GatewayApiController.RelayUplink
-- checks on every uplink.
--
-- WHY THIS IS MANUAL: see 2026-08-31-deviceDiagnostic-table.sql.
-- SAFE TO RE-RUN: ADD COLUMN uses IF NOT EXISTS.

ALTER TABLE `device`
  ADD COLUMN IF NOT EXISTS `LoRaPrivateKeyHex` VARCHAR(64) NULL,
  ADD COLUMN IF NOT EXISTS `LoRaLastUplinkCounter` BIGINT NULL;

-- Sanity check after running:
--   SHOW COLUMNS FROM `device` LIKE 'LoRa%';
