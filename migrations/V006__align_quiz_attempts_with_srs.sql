-- =============================================================================
-- Migration: V006__align_quiz_attempts_with_srs.sql
-- Feature:   Quiz Attempt schema alignment with SRS
-- Author:    BigO Dev Team
-- Date:      2026-04-01
-- =============================================================================
-- Purpose:
--   - Preserve the existing V005 tables
--   - Add the SRS fields missing from quiz_attempts
--   - Backfill the new timestamps from the legacy submitted_at column
--   - Rebuild indexes to favor the SRS completed_at field
--
-- Notes:
--   - The legacy passed/submitted_at columns are intentionally retained for
--     backward compatibility with the current codebase.
--   - No table is recreated here.
-- =============================================================================

ALTER TABLE quiz_attempts
    ADD COLUMN IF NOT EXISTS total_questions INT UNSIGNED NOT NULL DEFAULT 0
        AFTER score,
    ADD COLUMN IF NOT EXISTS xp_earned INT NOT NULL DEFAULT 0
        AFTER total_questions,
    ADD COLUMN IF NOT EXISTS started_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
        AFTER xp_earned,
    ADD COLUMN IF NOT EXISTS completed_at DATETIME NULL
        AFTER started_at;

-- Backfill the newly-added SRS timestamps from the legacy submitted_at column
-- so existing attempt rows remain semantically complete.
UPDATE quiz_attempts
SET started_at = COALESCE(started_at, submitted_at, CURRENT_TIMESTAMP),
    completed_at = COALESCE(completed_at, submitted_at)
WHERE completed_at IS NULL OR started_at IS NULL;

DROP INDEX IF EXISTS idx_quiz_attempts_user ON quiz_attempts;
CREATE INDEX idx_quiz_attempts_user
    ON quiz_attempts (user_id, completed_at);

DROP INDEX IF EXISTS idx_quiz_attempts_quiz ON quiz_attempts;
CREATE INDEX idx_quiz_attempts_quiz
    ON quiz_attempts (quiz_id, completed_at);
