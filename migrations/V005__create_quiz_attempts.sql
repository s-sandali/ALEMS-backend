-- =============================================================================
-- Migration: V005__create_quiz_attempts.sql
-- Feature:   Quiz Attempt & Grading
-- Author:    BigO Dev Team
-- Date:      2026-03-30
-- =============================================================================
-- Naming convention: snake_case (consistent with existing tables)
-- Idempotent:        safe to re-run; uses IF NOT EXISTS / DROP IF EXISTS
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Table: quiz_attempts
-- Records one submission of a quiz by a student.
-- score is stored as an integer percentage (0–100).
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS quiz_attempts (
    attempt_id      INT              NOT NULL AUTO_INCREMENT,
    quiz_id         INT              NOT NULL,
    user_id         INT              NOT NULL,              -- FK → Users(Id)
    score           TINYINT UNSIGNED NOT NULL,              -- percentage 0–100
    passed          BOOLEAN          NOT NULL,
    submitted_at    DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT pk_quiz_attempts          PRIMARY KEY (attempt_id),
    CONSTRAINT fk_quiz_attempts_quiz     FOREIGN KEY (quiz_id)
        REFERENCES quizzes (quiz_id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,
    CONSTRAINT fk_quiz_attempts_user     FOREIGN KEY (user_id)
        REFERENCES Users (Id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,
    CONSTRAINT chk_attempt_score         CHECK (score BETWEEN 0 AND 100)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='One row per quiz submission by a student.';

-- Index for common query: fetch all attempts by a user
DROP INDEX IF EXISTS idx_quiz_attempts_user ON quiz_attempts;
CREATE INDEX idx_quiz_attempts_user
    ON quiz_attempts (user_id, submitted_at);

-- Index for common query: fetch all attempts for a quiz
DROP INDEX IF EXISTS idx_quiz_attempts_quiz ON quiz_attempts;
CREATE INDEX idx_quiz_attempts_quiz
    ON quiz_attempts (quiz_id);

-- -----------------------------------------------------------------------------
-- Table: attempt_answers
-- Records the answer a student selected for each question in an attempt.
-- is_correct is denormalised at write time for efficient result queries.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS attempt_answers (
    answer_id       INT     NOT NULL AUTO_INCREMENT,
    attempt_id      INT     NOT NULL,
    question_id     INT     NOT NULL,
    selected_option CHAR(1) NOT NULL,                      -- 'A' | 'B' | 'C' | 'D'
    is_correct      BOOLEAN NOT NULL,

    CONSTRAINT pk_attempt_answers           PRIMARY KEY (answer_id),
    CONSTRAINT fk_attempt_answers_attempt   FOREIGN KEY (attempt_id)
        REFERENCES quiz_attempts (attempt_id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
    CONSTRAINT fk_attempt_answers_question  FOREIGN KEY (question_id)
        REFERENCES quiz_questions (question_id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,
    CONSTRAINT chk_selected_option          CHECK (selected_option IN ('A','B','C','D'))
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci
  COMMENT='Per-question answer record for a quiz attempt.';

-- Index for common query: fetch all answers for a given attempt
DROP INDEX IF EXISTS idx_attempt_answers_attempt ON attempt_answers;
CREATE INDEX idx_attempt_answers_attempt
    ON attempt_answers (attempt_id);
