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

-- Add new columns (no IF NOT EXISTS)
ALTER TABLE quiz_attempts
    ADD COLUMN total_questions INT UNSIGNED NOT NULL DEFAULT 0,
    ADD COLUMN xp_earned INT NOT NULL DEFAULT 0,
    ADD COLUMN started_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ADD COLUMN completed_at DATETIME NULL;

-- Backfill timestamps
UPDATE quiz_attempts
SET started_at = COALESCE(started_at, submitted_at, CURRENT_TIMESTAMP),
    completed_at = COALESCE(completed_at, submitted_at)
WHERE completed_at IS NULL OR started_at IS NULL;

-- Swap idx_quiz_attempts_user to completed_at.
-- Must create a temp index first so the FK on user_id is never left uncovered.
CREATE INDEX idx_quiz_attempts_user_tmp ON quiz_attempts (user_id);
DROP INDEX idx_quiz_attempts_user ON quiz_attempts;
CREATE INDEX idx_quiz_attempts_user ON quiz_attempts (user_id, completed_at);
DROP INDEX idx_quiz_attempts_user_tmp ON quiz_attempts;

-- Swap idx_quiz_attempts_quiz to completed_at (same pattern for quiz_id FK).
CREATE INDEX idx_quiz_attempts_quiz_tmp ON quiz_attempts (quiz_id);
DROP INDEX idx_quiz_attempts_quiz ON quiz_attempts;
CREATE INDEX idx_quiz_attempts_quiz ON quiz_attempts (quiz_id, completed_at);
DROP INDEX idx_quiz_attempts_quiz_tmp ON quiz_attempts;