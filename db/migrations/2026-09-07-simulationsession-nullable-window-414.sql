-- Roadmap #414 (2): a session is now created without a time window (Create is name-only) and only
-- gets StartedAtUtc/ExpiresAtUtc once Start is called separately from Details - both must accept NULL.
-- SAFE TO RE-RUN: MODIFY COLUMN restates the same nullability each time, no data touched.

ALTER TABLE `simulationSession` MODIFY COLUMN `StartedAtUtc` datetime(6) NULL;
ALTER TABLE `simulationSession` MODIFY COLUMN `ExpiresAtUtc` datetime(6) NULL;

-- Sanity check after running:
--   SHOW COLUMNS FROM simulationSession LIKE '%AtUtc';  -- expect StartedAtUtc/ExpiresAtUtc both YES nullable
