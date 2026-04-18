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

-- Recreate indexes on completed_at (drop old ones first if they exist)
SET @i1 = (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'quiz_attempts' AND INDEX_NAME = 'idx_quiz_attempts_user');
SET @d1 = IF(@i1 > 0, 'DROP INDEX idx_quiz_attempts_user ON quiz_attempts', 'SELECT 1');
PREPARE stmt FROM @d1; EXECUTE stmt; DEALLOCATE PREPARE stmt;
CREATE INDEX idx_quiz_attempts_user ON quiz_attempts (user_id, completed_at);

SET @i2 = (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'quiz_attempts' AND INDEX_NAME = 'idx_quiz_attempts_quiz');
SET @d2 = IF(@i2 > 0, 'DROP INDEX idx_quiz_attempts_quiz ON quiz_attempts', 'SELECT 1');
PREPARE stmt FROM @d2; EXECUTE stmt; DEALLOCATE PREPARE stmt;
CREATE INDEX idx_quiz_attempts_quiz ON quiz_attempts (quiz_id, completed_at);