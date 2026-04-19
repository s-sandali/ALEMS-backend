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

SET @col1 = (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'quiz_questions'
               AND COLUMN_NAME = 'question_type');
SET @sql1 = IF(@col1 = 0,
    "ALTER TABLE quiz_questions ADD COLUMN question_type ENUM('MCQ','PREDICT_STEP') NOT NULL DEFAULT 'MCQ' AFTER quiz_id",
    'SELECT 1');
PREPARE stmt FROM @sql1; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col2 = (SELECT COUNT(*) FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'quiz_questions'
               AND COLUMN_NAME = 'difficulty');
SET @sql2 = IF(@col2 = 0,
    "ALTER TABLE quiz_questions ADD COLUMN difficulty ENUM('easy','medium','hard') NOT NULL DEFAULT 'easy' AFTER correct_option",
    'SELECT 1');
PREPARE stmt FROM @sql2; EXECUTE stmt; DEALLOCATE PREPARE stmt;
