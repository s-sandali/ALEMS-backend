-- =============================================================================
-- Migration: V007__add_quiz_question_xp_reward.sql
-- Feature:   Quiz question XP rewards
-- Author:    BigO Dev Team
-- Date:      2026-04-01
-- =============================================================================
-- Adds the per-question XP reward used by quiz grading.
-- This migration is additive and does not recreate the table.
-- =============================================================================
ALTER TABLE quiz_questions
    ADD COLUMN xp_reward INT NOT NULL DEFAULT 0;

UPDATE quiz_questions
SET xp_reward = CASE difficulty
    WHEN 'easy' THEN 10
    WHEN 'medium' THEN 20
    WHEN 'hard' THEN 30
    ELSE 0
END;