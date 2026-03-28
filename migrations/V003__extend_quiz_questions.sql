-- =============================================================================
-- Migration: V003__extend_quiz_questions.sql
-- Feature:   Feature 1.1 – Extend quiz_questions for Enhanced MCQ System
-- Author:    BigO Dev Team
-- Date:      2026-03-28
-- =============================================================================
-- Adds two columns to quiz_questions:
--   question_type  – distinguishes MCQ from future PREDICT_STEP questions
--   difficulty     – easy / medium / hard, used for filtering and scoring
-- =============================================================================

ALTER TABLE quiz_questions
    ADD COLUMN IF NOT EXISTS question_type ENUM('MCQ','PREDICT_STEP') NOT NULL DEFAULT 'MCQ'
        AFTER quiz_id,
    ADD COLUMN IF NOT EXISTS difficulty    ENUM('easy','medium','hard') NOT NULL DEFAULT 'easy'
        AFTER correct_option;
