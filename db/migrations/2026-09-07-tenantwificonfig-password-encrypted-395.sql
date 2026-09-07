-- Roadmap #395(6): tenantWifiConfig.Password now stores SecretProtector.Protect's ciphertext
-- (DataProtection-encrypted at rest, GET /api/Discovery/WifiConfigs never returns it), not the
-- plaintext WiFi password. Ciphertext for a 63-char WPA2 password already runs ~170+ base64
-- chars, well past the old plaintext-sized VARCHAR(64) cap - widen it before the app writes one.
-- An existing plaintext row still reads back fine (SecretProtector.Unprotect falls back to
-- returning it as-is on a decrypt failure) and gets re-encrypted the next time it's saved.
--
-- WHY THIS IS MANUAL: see 2026-08-31-deviceDiagnostic-table.sql.
-- SAFE TO RE-RUN: MODIFY COLUMN is idempotent at the target width.

ALTER TABLE `tenantWifiConfig`
  MODIFY COLUMN `Password` VARCHAR(512) NOT NULL;

-- Sanity check after running:
--   SHOW COLUMNS FROM `tenantWifiConfig` LIKE 'Password';
