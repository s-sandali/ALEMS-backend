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
    attempt_id   INT NOT NULL AUTO_INCREMENT,
    quiz_id      INT NOT NULL,
    user_id      INT NOT NULL,
    score        TINYINT UNSIGNED NOT NULL,
    passed       TINYINT(1) NOT NULL,
    submitted_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    PRIMARY KEY (attempt_id),

    FOREIGN KEY (quiz_id)
        REFERENCES quizzes (quiz_id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,

    FOREIGN KEY (user_id)
        REFERENCES Users (Id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE INDEX idx_quiz_attempts_user
    ON quiz_attempts (user_id, submitted_at);

CREATE INDEX idx_quiz_attempts_quiz
    ON quiz_attempts (quiz_id);


CREATE TABLE IF NOT EXISTS attempt_answers (
    answer_id       INT NOT NULL AUTO_INCREMENT,
    attempt_id      INT NOT NULL,
    question_id     INT NOT NULL,
    selected_option ENUM('A','B','C','D') NOT NULL,
    is_correct      TINYINT(1) NOT NULL,

    PRIMARY KEY (answer_id),

    FOREIGN KEY (attempt_id)
        REFERENCES quiz_attempts (attempt_id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    FOREIGN KEY (question_id)
        REFERENCES quiz_questions (question_id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE INDEX idx_attempt_answers_attempt
    ON attempt_answers (attempt_id);