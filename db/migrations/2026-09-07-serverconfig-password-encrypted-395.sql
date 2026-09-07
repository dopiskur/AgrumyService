-- Roadmap #395(5): serverConfig.MqttPassword/EmailPassword now store SecretProtector.Protect's
-- ciphertext (DataProtection-encrypted at rest, GET /api/ServerConfig never returns either one),
-- not the plaintext broker/SMTP password. Ciphertext for even a short password already runs
-- ~90+ base64 chars, past the old plaintext-sized VARCHAR(255) cap for a longer one - widen both
-- before the app writes one. An existing plaintext row still reads back fine
-- (SecretProtector.Unprotect falls back to returning it as-is on a decrypt failure) and gets
-- re-encrypted the next time it's saved.
--
-- WHY THIS IS MANUAL: see 2026-08-31-deviceDiagnostic-table.sql.
-- SAFE TO RE-RUN: MODIFY COLUMN is idempotent at the target width.

ALTER TABLE `serverConfig`
  MODIFY COLUMN `MqttPassword` VARCHAR(512) NULL,
  MODIFY COLUMN `EmailPassword` VARCHAR(512) NULL;

-- Sanity check after running:
--   SHOW COLUMNS FROM `serverConfig` LIKE '%Password';
